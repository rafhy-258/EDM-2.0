using Edm.Configuration.Application.Limits;
using Edm.Configuration.Domain.Limits;
using Npgsql;

namespace Edm.Configuration.Infrastructure.Limits;

/// <summary>
/// PostgreSQL store for department-head limits. Each nature has its own table. The table name comes from a fixed
/// mapping, never from input. Every statement filters by tenant.
/// </summary>
public sealed class NpgsqlDeptHeadLimitStore(string connectionString) : IDeptHeadLimitStore
{
    private const string UniqueViolation = "23505";

    private const string Columns =
        "id, tenant_id, amount_pkr, description, effective_from, effective_to, is_active, row_version, updated_at, updated_by";

    public async Task<IReadOnlyList<DeptHeadLimit>> ListAsync(Guid tenantId, LimitNature nature, CancellationToken cancellationToken)
    {
        var sql = "SELECT " + Columns + " FROM " + TableFor(nature) + " "
            + "WHERE tenant_id = @tenant ORDER BY effective_from DESC, id";

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("tenant", tenantId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<DeptHeadLimit>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(Read(reader, nature));
        }

        return rows;
    }

    public async Task<bool> ReplaceAsync(
        Guid tenantId,
        LimitNature nature,
        DeptHeadLimit? closing,
        long? expectedClosingVersion,
        DeptHeadLimit created,
        CancellationToken cancellationToken)
    {
        var table = TableFor(nature);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            if (closing is not null)
            {
                const string close = "UPDATE {0} SET is_active = false, effective_to = @to, row_version = row_version + 1, "
                    + "updated_at = @at, updated_by = @by "
                    + "WHERE id = @id AND tenant_id = @tenant AND is_active AND row_version = @expected";

                var closed = await ExecuteAsync(
                    connection,
                    transaction,
                    string.Format(System.Globalization.CultureInfo.InvariantCulture, close, table),
                    cancellationToken,
                    ("id", closing.Id),
                    ("tenant", tenantId),
                    ("to", closing.EffectiveTo),
                    ("at", closing.UpdatedAtUtc),
                    ("by", closing.UpdatedBy),
                    ("expected", expectedClosingVersion));

                if (closed != 1)
                {
                    return false;
                }
            }

            var insert = "INSERT INTO " + table + " (id, tenant_id, amount_pkr, description, effective_from, effective_to, "
                + "is_active, row_version, created_at, created_by, updated_at, updated_by) "
                + "VALUES (@id, @tenant, @amount, @description, @from, NULL, true, @version, @at, @by, @at, @by)";

            await ExecuteAsync(
                connection,
                transaction,
                insert,
                cancellationToken,
                ("id", created.Id),
                ("tenant", tenantId),
                ("amount", created.AmountPkr),
                ("description", created.Description),
                ("from", created.EffectiveFrom),
                ("version", created.RowVersion),
                ("at", created.UpdatedAtUtc),
                ("by", created.UpdatedBy));

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
        {
            // A concurrent save already created the active row. The transaction rolls back when it is disposed.
            return false;
        }
    }

    public async Task<bool> DeactivateAsync(
        Guid tenantId,
        LimitNature nature,
        DeptHeadLimit closed,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var sql = "UPDATE " + TableFor(nature) + " SET is_active = false, effective_to = @to, "
            + "row_version = row_version + 1, updated_at = @at, updated_by = @by "
            + "WHERE id = @id AND tenant_id = @tenant AND is_active AND row_version = @expected";

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var rows = await ExecuteAsync(
            connection,
            null,
            sql,
            cancellationToken,
            ("id", closed.Id),
            ("tenant", tenantId),
            ("to", closed.EffectiveTo),
            ("at", closed.UpdatedAtUtc),
            ("by", closed.UpdatedBy),
            ("expected", expectedVersion));

        return rows == 1;
    }

    private static string TableFor(LimitNature nature)
        => nature == LimitNature.Opex ? "opex_dept_head_limits" : "capex_dept_head_limits";

    private static DeptHeadLimit Read(NpgsqlDataReader reader, LimitNature nature)
        => new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            nature,
            reader.GetDecimal(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
            reader.GetBoolean(6),
            reader.GetInt64(7),
            reader.GetFieldValue<DateTimeOffset>(8),
            reader.IsDBNull(9) ? null : reader.GetGuid(9));

    private static async Task<int> ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
