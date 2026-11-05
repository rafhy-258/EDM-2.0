using Edm.Configuration.Application.Authorities;
using Edm.Configuration.Domain.Authorities;
using Edm.SharedKernel.Errors;
using Npgsql;

namespace Edm.Configuration.Infrastructure.Authorities;

public sealed class NpgsqlApprovalAuthorityStore(string connectionString) : IApprovalAuthorityStore
{
    private const string UniqueViolation = "23505";
    private const string MemberDuplicateConstraint = "ec_members_user_designation_unique";
    private const string MemberHolderConstraint = "ec_members_one_active_holder";
    private const string AuthorityDuplicateConstraint = "recommending_authorities_department_user_active";

    private const string MemberColumns =
        "m.id, m.tenant_id, m.user_id, m.designation, m.is_active, m.row_version, m.updated_at, m.updated_by";
    private const string ActionTagColumns =
        "a.id, a.tenant_id, a.name, a.designation, a.allowed_actions, a.is_active, a.row_version, a.updated_at, a.updated_by";
    private const string AuthorityColumns =
        "r.id, r.tenant_id, r.title, r.department_id, r.user_id, r.default_action_tag_set_id, r.is_active, r.row_version, r.updated_at, r.updated_by";

    public async Task EnsureDefaultActionTagSetsAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO action_tag_sets (id, tenant_id, name, designation, allowed_actions, created_at, updated_at)
            VALUES
                (@dept_id, @tenant, 'Department Head', 'DEPARTMENT_HEAD', ARRAY['APPROVE', 'FORWARD', 'REJECT', 'REVERT'], @now, @now),
                (@ra_id, @tenant, 'Recommending Authority', 'RECOMMENDING_AUTHORITY', ARRAY['APPROVE', 'REJECT', 'REVERT'], @now, @now),
                (@ec_id, @tenant, 'EC Member', 'EC_MEMBER', ARRAY['APPROVE', 'REJECT', 'REVERT'], @now, @now),
                (@ceo_id, @tenant, 'CEO', 'CEO', ARRAY['APPROVE', 'REJECT', 'REVERT'], @now, @now),
                (@cfo_id, @tenant, 'CFO', 'CFO', ARRAY['APPROVE', 'REJECT', 'REVERT'], @now, @now),
                (@head_id, @tenant, 'Head of Admin & GST', 'HEAD_ADMIN_GST', ARRAY['APPROVE', 'REJECT', 'REVERT'], @now, @now)
            ON CONFLICT (tenant_id, designation) DO NOTHING
            """;

        await ExecuteAsync(
            sql,
            cancellationToken,
            ("tenant", tenantId),
            ("now", now),
            ("dept_id", Guid.CreateVersion7()),
            ("ra_id", Guid.CreateVersion7()),
            ("ec_id", Guid.CreateVersion7()),
            ("ceo_id", Guid.CreateVersion7()),
            ("cfo_id", Guid.CreateVersion7()),
            ("head_id", Guid.CreateVersion7()));
    }

    public async Task<IReadOnlyList<ActionTagSet>> ListActionTagSetsAsync(Guid tenantId, CancellationToken cancellationToken)
        => await QueryAsync(
            "SELECT " + ActionTagColumns + " FROM action_tag_sets a WHERE a.tenant_id = @tenant ORDER BY a.designation",
            ReadActionTagSet,
            cancellationToken,
            ("tenant", tenantId));

    public async Task<ActionTagSet?> FindActionTagSetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(
            "SELECT " + ActionTagColumns + " FROM action_tag_sets a WHERE a.tenant_id = @tenant AND a.id = @id",
            ReadActionTagSet,
            cancellationToken,
            ("tenant", tenantId),
            ("id", id));
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<bool> UpdateActionTagSetAsync(ActionTagSet set, long expectedVersion, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE action_tag_sets
            SET allowed_actions = @actions, row_version = row_version + 1, updated_at = @at, updated_by = @by
            WHERE id = @id AND tenant_id = @tenant AND row_version = @expected
            """;
        return await ExecuteAsync(
            sql, cancellationToken,
            ("actions", set.AllowedActions.ToArray()),
            ("at", set.UpdatedAtUtc),
            ("by", set.UpdatedBy),
            ("id", set.Id),
            ("tenant", set.TenantId),
            ("expected", expectedVersion)) == 1;
    }

    public async Task<IReadOnlyList<EcMember>> ListEcMembersAsync(
        Guid tenantId,
        string? designation,
        bool includeInactive,
        string? search,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT m.id, m.tenant_id, m.user_id, m.designation, m.is_active, m.row_version, m.updated_at, m.updated_by
            FROM ec_members m
            JOIN users u ON u.id = m.user_id AND u.tenant_id = m.tenant_id
            WHERE m.tenant_id = @tenant AND m.deleted_at IS NULL
              AND (CAST(@designation AS text) IS NULL OR m.designation = @designation)
              AND (@include_inactive OR m.is_active)
              AND (CAST(@search AS text) IS NULL OR strpos(lower(u.name), lower(CAST(@search AS text))) > 0
                   OR strpos(lower(u.employee_id), lower(CAST(@search AS text))) > 0
                   OR strpos(lower(m.designation), lower(CAST(@search AS text))) > 0)
            ORDER BY m.designation, lower(u.name)
            """;
        return await QueryAsync(
            sql,
            ReadMember,
            cancellationToken,
            ("tenant", tenantId),
            ("designation", designation),
            ("include_inactive", includeInactive),
            ("search", search));
    }

    public async Task<EcMember?> FindEcMemberAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(
            "SELECT " + MemberColumns + " FROM ec_members m WHERE m.tenant_id = @tenant AND m.id = @id AND m.deleted_at IS NULL",
            ReadMember,
            cancellationToken,
            ("tenant", tenantId),
            ("id", id));
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<EcMember?> FindEcMemberAsync(
        Guid tenantId,
        Guid userId,
        string designation,
        CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(
            "SELECT " + MemberColumns + " FROM ec_members m "
            + "WHERE m.tenant_id = @tenant AND m.user_id = @user AND m.designation = @designation AND m.deleted_at IS NULL",
            ReadMember,
            cancellationToken,
            ("tenant", tenantId),
            ("user", userId),
            ("designation", designation));
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<bool> HasActiveHolderAsync(Guid tenantId, string designation, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM ec_members
                WHERE tenant_id = @tenant AND designation = @designation AND is_active AND deleted_at IS NULL
            )
            """;
        return (bool)(await ScalarAsync(
            sql, cancellationToken, ("tenant", tenantId), ("designation", designation)))!;
    }

    public async Task InsertEcMemberAsync(EcMember member, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO ec_members
                (id, tenant_id, user_id, designation, is_active, row_version, created_at, created_by, updated_at, updated_by)
            VALUES (@id, @tenant, @user, @designation, @active, @version, @at, @by, @at, @by)
            """;
        try
        {
            await ExecuteAsync(sql, cancellationToken,
                ("id", member.Id), ("tenant", member.TenantId), ("user", member.UserId),
                ("designation", member.Designation), ("active", member.IsActive), ("version", member.RowVersion),
                ("at", member.UpdatedAtUtc), ("by", member.UpdatedBy));
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
        {
            ThrowMemberConflict(ex.ConstraintName);
            throw;
        }
    }

    public async Task<bool> UpdateEcMemberAsync(EcMember member, long expectedVersion, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE ec_members
            SET is_active = @active, row_version = row_version + 1, updated_at = @at, updated_by = @by
            WHERE id = @id AND tenant_id = @tenant AND deleted_at IS NULL AND row_version = @expected
            """;
        try
        {
            return await ExecuteAsync(sql, cancellationToken,
                ("active", member.IsActive), ("at", member.UpdatedAtUtc), ("by", member.UpdatedBy),
                ("id", member.Id), ("tenant", member.TenantId), ("expected", expectedVersion)) == 1;
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
        {
            ThrowMemberConflict(ex.ConstraintName);
            throw;
        }
    }

    public async Task<bool> SoftDeleteEcMemberAsync(
        Guid tenantId,
        Guid id,
        long expectedVersion,
        Guid? deletedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        => await ExecuteAsync(
            """
            UPDATE ec_members
            SET deleted_at = @at, deleted_by = @by, row_version = row_version + 1, updated_at = @at, updated_by = @by
            WHERE id = @id AND tenant_id = @tenant AND deleted_at IS NULL AND row_version = @expected
            """,
            cancellationToken,
            ("at", now), ("by", deletedBy), ("id", id), ("tenant", tenantId), ("expected", expectedVersion)) == 1;

    public async Task<IReadOnlyList<RecommendingAuthority>> ListRecommendingAuthoritiesAsync(
        Guid tenantId,
        bool includeInactive,
        string? search,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT r.id, r.tenant_id, r.title, r.department_id, r.user_id, r.default_action_tag_set_id,
                   r.is_active, r.row_version, r.updated_at, r.updated_by
            FROM recommending_authorities r
            JOIN departments d ON d.id = r.department_id AND d.tenant_id = r.tenant_id
            JOIN users u ON u.id = r.user_id AND u.tenant_id = r.tenant_id
            WHERE r.tenant_id = @tenant AND r.deleted_at IS NULL
              AND (@include_inactive OR r.is_active)
              AND (CAST(@search AS text) IS NULL OR strpos(lower(r.title), lower(CAST(@search AS text))) > 0
                   OR strpos(lower(u.name), lower(CAST(@search AS text))) > 0
                   OR strpos(lower(u.employee_id), lower(CAST(@search AS text))) > 0
                   OR strpos(lower(d.name), lower(CAST(@search AS text))) > 0)
            ORDER BY lower(d.name), lower(r.title), lower(u.name)
            """;
        return await QueryAsync(sql, ReadAuthority, cancellationToken,
            ("tenant", tenantId), ("include_inactive", includeInactive), ("search", search));
    }

    public async Task<RecommendingAuthority?> FindRecommendingAuthorityAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(
            "SELECT " + AuthorityColumns + " FROM recommending_authorities r "
            + "WHERE r.tenant_id = @tenant AND r.id = @id AND r.deleted_at IS NULL",
            ReadAuthority,
            cancellationToken,
            ("tenant", tenantId),
            ("id", id));
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<bool> RecommendingAuthorityTakenAsync(
        Guid tenantId,
        Guid departmentId,
        Guid userId,
        Guid exceptId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM recommending_authorities
                WHERE tenant_id = @tenant AND department_id = @department AND user_id = @user
                  AND is_active AND deleted_at IS NULL AND id <> @except
            )
            """;
        return (bool)(await ScalarAsync(sql, cancellationToken,
            ("tenant", tenantId), ("department", departmentId), ("user", userId), ("except", exceptId)))!;
    }

    public async Task<Guid?> FindActionTagSetIdAsync(
        Guid tenantId,
        string designation,
        CancellationToken cancellationToken)
    {
        var value = await ScalarAsync(
            "SELECT id FROM action_tag_sets WHERE tenant_id = @tenant AND designation = @designation AND is_active",
            cancellationToken,
            ("tenant", tenantId),
            ("designation", designation));
        return value is null ? null : (Guid)value;
    }

    public async Task InsertRecommendingAuthorityAsync(RecommendingAuthority authority, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO recommending_authorities
                (id, tenant_id, title, department_id, user_id, default_action_tag_set_id, is_active, row_version,
                 created_at, created_by, updated_at, updated_by)
            VALUES (@id, @tenant, @title, @department, @user, @tags, @active, @version, @at, @by, @at, @by)
            """;
        try
        {
            await ExecuteAsync(sql, cancellationToken, AuthorityParameters(authority));
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation && ex.ConstraintName == AuthorityDuplicateConstraint)
        {
            ThrowAuthorityConflict();
        }
    }

    public async Task<bool> UpdateRecommendingAuthorityAsync(
        RecommendingAuthority authority,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE recommending_authorities
            SET department_id = @department, user_id = @user, is_active = @active,
                row_version = row_version + 1, updated_at = @at, updated_by = @by
            WHERE id = @id AND tenant_id = @tenant AND deleted_at IS NULL AND row_version = @expected
            """;
        try
        {
            return await ExecuteAsync(sql, cancellationToken,
                ("department", authority.DepartmentId), ("user", authority.UserId), ("active", authority.IsActive),
                ("at", authority.UpdatedAtUtc), ("by", authority.UpdatedBy), ("id", authority.Id),
                ("tenant", authority.TenantId), ("expected", expectedVersion)) == 1;
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation && ex.ConstraintName == AuthorityDuplicateConstraint)
        {
            ThrowAuthorityConflict();
            throw;
        }
    }

    public async Task<bool> SoftDeleteRecommendingAuthorityAsync(
        Guid tenantId,
        Guid id,
        long expectedVersion,
        Guid? deletedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        => await ExecuteAsync(
            """
            UPDATE recommending_authorities
            SET deleted_at = @at, deleted_by = @by, row_version = row_version + 1, updated_at = @at, updated_by = @by
            WHERE id = @id AND tenant_id = @tenant AND deleted_at IS NULL AND row_version = @expected
            """,
            cancellationToken,
            ("at", now), ("by", deletedBy), ("id", id), ("tenant", tenantId), ("expected", expectedVersion)) == 1;

    private static (string Name, object? Value)[] AuthorityParameters(RecommendingAuthority authority) =>
    [
        ("id", authority.Id), ("tenant", authority.TenantId), ("title", authority.Title),
        ("department", authority.DepartmentId), ("user", authority.UserId),
        ("tags", authority.DefaultActionTagSetId), ("active", authority.IsActive),
        ("version", authority.RowVersion), ("at", authority.UpdatedAtUtc), ("by", authority.UpdatedBy),
    ];

    private static void ThrowMemberConflict(string? constraintName)
    {
        if (constraintName == MemberDuplicateConstraint)
        {
            ThrowFieldError("userId", "This member already exists with this designation. Reactivate the existing record instead.");
        }

        if (constraintName == MemberHolderConstraint)
        {
            throw new EdmException(EdmErrorCodes.SingleHolderAlreadyActive);
        }
    }

    private static void ThrowAuthorityConflict()
        => ThrowFieldError("userId", "This department and user already have a recommending authority.");

    private static void ThrowFieldError(string field, string message)
        => throw new EdmException(EdmErrorCodes.ValidationFailed, new Dictionary<string, string[]> { [field] = [message] });

    private static EcMember ReadMember(NpgsqlDataReader reader)
        => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetBoolean(4),
            reader.GetInt64(5), reader.GetFieldValue<DateTimeOffset>(6), reader.IsDBNull(7) ? null : reader.GetGuid(7));

    private static ActionTagSet ReadActionTagSet(NpgsqlDataReader reader)
        => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
            reader.GetFieldValue<string[]>(4), reader.GetBoolean(5), reader.GetInt64(6),
            reader.GetFieldValue<DateTimeOffset>(7), reader.IsDBNull(8) ? null : reader.GetGuid(8));

    private static RecommendingAuthority ReadAuthority(NpgsqlDataReader reader)
        => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetGuid(3), reader.GetGuid(4),
            reader.GetGuid(5), reader.GetBoolean(6), reader.GetInt64(7), reader.GetFieldValue<DateTimeOffset>(8),
            reader.IsDBNull(9) ? null : reader.GetGuid(9));

    private async Task<List<T>> QueryAsync<T>(
        string sql,
        Func<NpgsqlDataReader, T> map,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        AddParameters(command, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    private async Task<int> ExecuteAsync(
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        AddParameters(command, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<object?> ScalarAsync(
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        AddParameters(command, parameters);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static void AddParameters(NpgsqlCommand command, (string Name, object? Value)[] parameters)
    {
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
    }
}
