using Edm.Identity.Application;
using Edm.Identity.Domain;
using Npgsql;
using NpgsqlTypes;

namespace Edm.Identity.Infrastructure;

/// <summary>Account store on PostgreSQL. Parameterised SQL only. Every query is filtered by tenant.</summary>
public sealed class NpgsqlUserAccountStore(string connectionString) : IUserAccountStore
{
    private const string SelectUser = """
        SELECT u.id, u.tenant_id, u.username, u.display_name, u.department, u.password_hash,
               u.is_active, u.failed_sign_ins, u.locked_until_utc,
               COALESCE(ARRAY(SELECT r.role_key FROM identity_user_roles r WHERE r.user_id = u.id ORDER BY r.role_key), '{}') AS roles
        FROM identity_users u
        """;

    public async Task<UserAccount?> FindByUsernameAsync(Guid tenantId, string username, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(SelectUser + " WHERE u.tenant_id = @tenant AND u.username = @username", connection);
        command.Parameters.AddWithValue("tenant", tenantId);
        command.Parameters.AddWithValue("username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Read(reader);
    }

    public async Task<bool> AnyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM identity_users WHERE tenant_id = @tenant)", connection);
        command.Parameters.AddWithValue("tenant", tenantId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task AddAsync(UserAccount user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var insert = new NpgsqlCommand(
            """
            INSERT INTO identity_users (id, tenant_id, username, display_name, department, password_hash, is_active, failed_sign_ins, locked_until_utc)
            VALUES (@id, @tenant, @username, @displayName, @department, @passwordHash, @isActive, @failed, @lockedUntil)
            """,
            connection,
            transaction))
        {
            insert.Parameters.AddWithValue("id", user.Id);
            insert.Parameters.AddWithValue("tenant", user.TenantId);
            insert.Parameters.AddWithValue("username", user.Username);
            insert.Parameters.AddWithValue("displayName", user.DisplayName);
            insert.Parameters.Add(new NpgsqlParameter("department", NpgsqlDbType.Text) { Value = (object?)user.Department ?? DBNull.Value });
            insert.Parameters.AddWithValue("passwordHash", user.PasswordHash);
            insert.Parameters.AddWithValue("isActive", user.IsActive);
            insert.Parameters.AddWithValue("failed", user.FailedSignIns);
            insert.Parameters.Add(new NpgsqlParameter("lockedUntil", NpgsqlDbType.TimestampTz) { Value = (object?)user.LockedUntilUtc ?? DBNull.Value });
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var role in user.Roles)
        {
            await using var roleInsert = new NpgsqlCommand(
                "INSERT INTO identity_user_roles (tenant_id, user_id, role_key) VALUES (@tenant, @user, @role)",
                connection,
                transaction);
            roleInsert.Parameters.AddWithValue("tenant", user.TenantId);
            roleInsert.Parameters.AddWithValue("user", user.Id);
            roleInsert.Parameters.AddWithValue("role", role);
            await roleInsert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveSignInStateAsync(UserAccount user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "UPDATE identity_users SET failed_sign_ins = @failed, locked_until_utc = @lockedUntil WHERE tenant_id = @tenant AND id = @id",
            connection);
        command.Parameters.AddWithValue("failed", user.FailedSignIns);
        command.Parameters.Add(new NpgsqlParameter("lockedUntil", NpgsqlDbType.TimestampTz) { Value = (object?)user.LockedUntilUtc ?? DBNull.Value });
        command.Parameters.AddWithValue("tenant", user.TenantId);
        command.Parameters.AddWithValue("id", user.Id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static UserAccount Read(NpgsqlDataReader reader)
    {
        var lockedUntil = reader.IsDBNull(8) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(8);
        return UserAccount.Restore(
            id: reader.GetGuid(0),
            tenantId: reader.GetGuid(1),
            username: reader.GetString(2),
            displayName: reader.GetString(3),
            department: reader.IsDBNull(4) ? null : reader.GetString(4),
            passwordHash: reader.GetString(5),
            isActive: reader.GetBoolean(6),
            failedSignIns: reader.GetInt32(7),
            lockedUntilUtc: lockedUntil,
            roles: reader.GetFieldValue<string[]>(9));
    }
}
