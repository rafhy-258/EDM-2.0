using Edm.Identity.Application.Organisation;
using Edm.Identity.Domain.Organisation;
using Edm.SharedKernel.Errors;
using Npgsql;

namespace Edm.Identity.Infrastructure.Organisation;

/// <summary>
/// PostgreSQL store for departments and directory users. Every statement filters by tenant. A unique-index violation
/// (two requests racing for the same name or employee ID) becomes the same field error the service gives.
/// </summary>
public sealed class NpgsqlOrganisationStore(string connectionString) : IOrganisationStore
{
    private const string UniqueViolation = "23505";
    private const string DepartmentNameMessage = "A department with this name already exists.";
    private const string EmployeeIdMessage =
        "This employee ID already exists. Reactivate the existing record instead.";

    private const string DepartmentColumns =
        "d.id, d.tenant_id, d.name, d.is_active, d.row_version, d.updated_at, d.updated_by";

    private const string UserColumns =
        "u.id, u.tenant_id, u.employee_id, u.name, u.email, u.department_id, u.is_active, u.row_version, u.updated_at, u.updated_by";

    public async Task<IReadOnlyList<Department>> ListDepartmentsAsync(
        Guid tenantId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT " + DepartmentColumns + " FROM departments d "
            + "WHERE d.tenant_id = @tenant AND (@include_inactive OR d.is_active) "
            + "ORDER BY lower(d.name)";

        return await QueryAsync(
            sql,
            ReadDepartment,
            cancellationToken,
            ("tenant", tenantId),
            ("include_inactive", includeInactive));
    }

    public async Task<Department?> FindDepartmentAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var sql = "SELECT " + DepartmentColumns + " FROM departments d WHERE d.tenant_id = @tenant AND d.id = @id";
        var rows = await QueryAsync(sql, ReadDepartment, cancellationToken, ("tenant", tenantId), ("id", id));
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<bool> DepartmentNameTakenAsync(Guid tenantId, string name, Guid exceptId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT EXISTS (SELECT 1 FROM departments "
            + "WHERE tenant_id = @tenant AND is_active AND lower(name) = lower(@name) AND id <> @except_id)";

        var value = await ScalarAsync(sql, cancellationToken, ("tenant", tenantId), ("name", name), ("except_id", exceptId));
        return (bool)value!;
    }

    public async Task<int> CountActiveUsersInDepartmentAsync(Guid tenantId, Guid departmentId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT count(*) FROM users "
            + "WHERE tenant_id = @tenant AND department_id = @department AND is_active";

        var value = await ScalarAsync(sql, cancellationToken, ("tenant", tenantId), ("department", departmentId));
        return (int)(long)value!;
    }

    public async Task InsertDepartmentAsync(Department department, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO departments (id, tenant_id, name, is_active, row_version, "
            + "created_at, created_by, updated_at, updated_by) "
            + "VALUES (@id, @tenant, @name, @active, @version, @at, @by, @at, @by)";

        try
        {
            await ExecuteAsync(
                sql,
                cancellationToken,
                ("id", department.Id),
                ("tenant", department.TenantId),
                ("name", department.Name),
                ("active", department.IsActive),
                ("version", department.RowVersion),
                ("at", department.UpdatedAtUtc),
                ("by", department.UpdatedBy));
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
        {
            throw DepartmentNameConflict();
        }
    }

    public async Task<bool> UpdateDepartmentAsync(Department department, long expectedVersion, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE departments SET name = @name, is_active = @active, "
            + "row_version = row_version + 1, updated_at = @at, updated_by = @by "
            + "WHERE id = @id AND tenant_id = @tenant AND row_version = @expected";

        try
        {
            var rows = await ExecuteAsync(
                sql,
                cancellationToken,
                ("id", department.Id),
                ("tenant", department.TenantId),
                ("name", department.Name),
                ("active", department.IsActive),
                ("at", department.UpdatedAtUtc),
                ("by", department.UpdatedBy),
                ("expected", expectedVersion));
            return rows == 1;
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
        {
            throw DepartmentNameConflict();
        }
    }

    public async Task<IReadOnlyList<DirectoryUser>> ListUsersAsync(
        Guid tenantId,
        Guid? departmentId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT " + UserColumns + " FROM users u "
            + "WHERE u.tenant_id = @tenant AND (@include_inactive OR u.is_active)";

        var parameters = new List<(string Name, object? Value)>
        {
            ("tenant", tenantId),
            ("include_inactive", includeInactive),
        };

        if (departmentId is not null)
        {
            sql += " AND u.department_id = @department";
            parameters.Add(("department", departmentId.Value));
        }

        sql += " ORDER BY lower(u.name)";

        return await QueryAsync(sql, ReadUser, cancellationToken, parameters.ToArray());
    }

    public async Task<DirectoryUser?> FindUserAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var sql = "SELECT " + UserColumns + " FROM users u WHERE u.tenant_id = @tenant AND u.id = @id";
        var rows = await QueryAsync(sql, ReadUser, cancellationToken, ("tenant", tenantId), ("id", id));
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<bool> EmployeeIdTakenAsync(Guid tenantId, string employeeId, Guid exceptId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT EXISTS (SELECT 1 FROM users "
            + "WHERE tenant_id = @tenant AND lower(employee_id) = lower(@employee_id) AND id <> @except_id)";

        var value = await ScalarAsync(
            sql,
            cancellationToken,
            ("tenant", tenantId),
            ("employee_id", employeeId),
            ("except_id", exceptId));
        return (bool)value!;
    }

    public async Task InsertUserAsync(DirectoryUser user, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO users (id, tenant_id, employee_id, name, email, department_id, is_active, row_version, "
            + "created_at, created_by, updated_at, updated_by) "
            + "VALUES (@id, @tenant, @employee_id, @name, @email, @department, @active, @version, @at, @by, @at, @by)";

        try
        {
            await ExecuteAsync(
                sql,
                cancellationToken,
                ("id", user.Id),
                ("tenant", user.TenantId),
                ("employee_id", user.EmployeeId),
                ("name", user.Name),
                ("email", user.Email),
                ("department", user.DepartmentId),
                ("active", user.IsActive),
                ("version", user.RowVersion),
                ("at", user.UpdatedAtUtc),
                ("by", user.UpdatedBy));
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
        {
            throw EmployeeIdConflict();
        }
    }

    public async Task<bool> UpdateUserAsync(DirectoryUser user, long expectedVersion, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE users SET employee_id = @employee_id, name = @name, email = @email, "
            + "department_id = @department, is_active = @active, row_version = row_version + 1, "
            + "updated_at = @at, updated_by = @by "
            + "WHERE id = @id AND tenant_id = @tenant AND row_version = @expected";

        try
        {
            var rows = await ExecuteAsync(
                sql,
                cancellationToken,
                ("id", user.Id),
                ("tenant", user.TenantId),
                ("employee_id", user.EmployeeId),
                ("name", user.Name),
                ("email", user.Email),
                ("department", user.DepartmentId),
                ("active", user.IsActive),
                ("at", user.UpdatedAtUtc),
                ("by", user.UpdatedBy),
                ("expected", expectedVersion));
            return rows == 1;
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
        {
            throw EmployeeIdConflict();
        }
    }

    private static Department ReadDepartment(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetBoolean(3),
            reader.GetInt64(4),
            reader.GetFieldValue<DateTimeOffset>(5),
            reader.IsDBNull(6) ? null : reader.GetGuid(6));

    private static DirectoryUser ReadUser(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetGuid(5),
            reader.GetBoolean(6),
            reader.GetInt64(7),
            reader.GetFieldValue<DateTimeOffset>(8),
            reader.IsDBNull(9) ? null : reader.GetGuid(9));

    private static EdmException DepartmentNameConflict()
        => new(EdmErrorCodes.ValidationFailed, new Dictionary<string, string[]> { ["name"] = [DepartmentNameMessage] });

    private static EdmException EmployeeIdConflict()
        => new(EdmErrorCodes.ValidationFailed, new Dictionary<string, string[]> { ["employeeId"] = [EmployeeIdMessage] });

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<NpgsqlDataReader, T> map,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    private async Task<object?> ScalarAsync(
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private async Task<int> ExecuteAsync(
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
