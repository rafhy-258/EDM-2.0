using Edm.Configuration.Application.Catalogue;
using Edm.Configuration.Domain.Catalogue;
using Edm.SharedKernel.Errors;
using Npgsql;

namespace Edm.Configuration.Infrastructure.Catalogue;

/// <summary>
/// PostgreSQL store for categories and subcategories. Every statement filters by tenant and by deleted_at IS NULL.
/// A unique-index violation (two requests racing for the same name) becomes the same field error the service gives.
/// </summary>
public sealed class NpgsqlCatalogueStore(string connectionString) : ICatalogueStore
{
    private const string UniqueViolation = "23505";
    private const string CategoryNameMessage = "A category with this name already exists.";
    private const string SubcategoryNameMessage = "A subcategory with this name already exists in this category.";

    private const string CategoryColumns =
        "c.id, c.tenant_id, c.name, c.description, c.is_active, c.row_version, c.updated_at, c.updated_by";

    private const string SubcategoryColumns =
        "s.id, s.tenant_id, s.category_id, s.name, s.description, s.is_active, s.row_version, s.updated_at, s.updated_by";

    public async Task<IReadOnlyList<CategoryView>> ListCategoriesAsync(
        Guid tenantId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT " + CategoryColumns + ", "
            + "(SELECT count(*) FROM subcategories s WHERE s.category_id = c.id AND s.deleted_at IS NULL) AS subcategory_count "
            + "FROM categories c "
            + "WHERE c.tenant_id = @tenant AND c.deleted_at IS NULL AND (@include_inactive OR c.is_active) "
            + "ORDER BY lower(c.name)";

        return await QueryAsync(
            sql,
            reader => new CategoryView(ReadCategory(reader), (int)reader.GetInt64(8)),
            cancellationToken,
            ("tenant", tenantId),
            ("include_inactive", includeInactive));
    }

    public async Task<Category?> FindCategoryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var sql = "SELECT " + CategoryColumns + " FROM categories c "
            + "WHERE c.tenant_id = @tenant AND c.id = @id AND c.deleted_at IS NULL";

        var rows = await QueryAsync(sql, ReadCategory, cancellationToken, ("tenant", tenantId), ("id", id));
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<bool> CategoryNameTakenAsync(Guid tenantId, string name, Guid exceptId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT EXISTS (SELECT 1 FROM categories "
            + "WHERE tenant_id = @tenant AND deleted_at IS NULL AND is_active "
            + "AND lower(name) = lower(@name) AND id <> @except_id)";

        var value = await ScalarAsync(sql, cancellationToken, ("tenant", tenantId), ("name", name), ("except_id", exceptId));
        return (bool)value!;
    }

    public async Task<int> CountSubcategoriesAsync(Guid tenantId, Guid categoryId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT count(*) FROM subcategories "
            + "WHERE tenant_id = @tenant AND category_id = @category_id AND deleted_at IS NULL";

        var value = await ScalarAsync(sql, cancellationToken, ("tenant", tenantId), ("category_id", categoryId));
        return (int)(long)value!;
    }

    public async Task InsertCategoryAsync(Category category, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO categories (id, tenant_id, name, description, is_active, row_version, "
            + "created_at, created_by, updated_at, updated_by) "
            + "VALUES (@id, @tenant, @name, @description, @active, @version, @at, @by, @at, @by)";

        try
        {
            await ExecuteAsync(
                sql,
                cancellationToken,
                ("id", category.Id),
                ("tenant", category.TenantId),
                ("name", category.Name),
                ("description", category.Description),
                ("active", category.IsActive),
                ("version", category.RowVersion),
                ("at", category.UpdatedAtUtc),
                ("by", category.UpdatedBy));
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
        {
            throw NameTaken("name", CategoryNameMessage);
        }
    }

    public async Task<bool> UpdateCategoryAsync(Category category, long expectedRowVersion, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE categories SET name = @name, description = @description, is_active = @active, "
            + "row_version = row_version + 1, updated_at = @at, updated_by = @by "
            + "WHERE id = @id AND tenant_id = @tenant AND deleted_at IS NULL AND row_version = @expected";

        try
        {
            var rows = await ExecuteAsync(
                sql,
                cancellationToken,
                ("id", category.Id),
                ("tenant", category.TenantId),
                ("name", category.Name),
                ("description", category.Description),
                ("active", category.IsActive),
                ("at", category.UpdatedAtUtc),
                ("by", category.UpdatedBy),
                ("expected", expectedRowVersion));
            return rows == 1;
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
        {
            throw NameTaken("name", CategoryNameMessage);
        }
    }

    public async Task<bool> SoftDeleteCategoryAsync(
        Guid tenantId,
        Guid id,
        long expectedRowVersion,
        Guid? deletedBy,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = "UPDATE categories SET deleted_at = @at, deleted_by = @by, "
            + "row_version = row_version + 1, updated_at = @at, updated_by = @by "
            + "WHERE id = @id AND tenant_id = @tenant AND deleted_at IS NULL AND row_version = @expected";

        var rows = await ExecuteAsync(
            sql,
            cancellationToken,
            ("id", id),
            ("tenant", tenantId),
            ("at", deletedAtUtc),
            ("by", deletedBy),
            ("expected", expectedRowVersion));
        return rows == 1;
    }

    public async Task<IReadOnlyList<SubcategoryView>> ListSubcategoriesAsync(
        Guid tenantId,
        Guid? categoryId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT " + SubcategoryColumns + ", c.name "
            + "FROM subcategories s JOIN categories c ON c.id = s.category_id "
            + "WHERE s.tenant_id = @tenant AND s.deleted_at IS NULL "
            + "AND (NOT @filter_category OR s.category_id = @category_id) "
            + "AND (@include_inactive OR s.is_active) "
            + "ORDER BY lower(c.name), lower(s.name)";

        return await QueryAsync(
            sql,
            reader => new SubcategoryView(ReadSubcategory(reader), reader.GetString(9)),
            cancellationToken,
            ("tenant", tenantId),
            ("filter_category", categoryId is not null),
            ("category_id", categoryId ?? Guid.Empty),
            ("include_inactive", includeInactive));
    }

    public async Task<Subcategory?> FindSubcategoryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var sql = "SELECT " + SubcategoryColumns + " FROM subcategories s "
            + "WHERE s.tenant_id = @tenant AND s.id = @id AND s.deleted_at IS NULL";

        var rows = await QueryAsync(sql, ReadSubcategory, cancellationToken, ("tenant", tenantId), ("id", id));
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<bool> SubcategoryNameTakenAsync(
        Guid tenantId,
        Guid categoryId,
        string name,
        Guid exceptId,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT EXISTS (SELECT 1 FROM subcategories "
            + "WHERE tenant_id = @tenant AND category_id = @category_id AND deleted_at IS NULL AND is_active "
            + "AND lower(name) = lower(@name) AND id <> @except_id)";

        var value = await ScalarAsync(
            sql,
            cancellationToken,
            ("tenant", tenantId),
            ("category_id", categoryId),
            ("name", name),
            ("except_id", exceptId));
        return (bool)value!;
    }

    public async Task InsertSubcategoryAsync(Subcategory subcategory, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO subcategories (id, tenant_id, category_id, name, description, is_active, row_version, "
            + "created_at, created_by, updated_at, updated_by) "
            + "VALUES (@id, @tenant, @category_id, @name, @description, @active, @version, @at, @by, @at, @by)";

        try
        {
            await ExecuteAsync(
                sql,
                cancellationToken,
                ("id", subcategory.Id),
                ("tenant", subcategory.TenantId),
                ("category_id", subcategory.CategoryId),
                ("name", subcategory.Name),
                ("description", subcategory.Description),
                ("active", subcategory.IsActive),
                ("version", subcategory.RowVersion),
                ("at", subcategory.UpdatedAtUtc),
                ("by", subcategory.UpdatedBy));
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
        {
            throw NameTaken("name", SubcategoryNameMessage);
        }
    }

    public async Task<bool> UpdateSubcategoryAsync(Subcategory subcategory, long expectedRowVersion, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE subcategories SET name = @name, description = @description, is_active = @active, "
            + "row_version = row_version + 1, updated_at = @at, updated_by = @by "
            + "WHERE id = @id AND tenant_id = @tenant AND deleted_at IS NULL AND row_version = @expected";

        try
        {
            var rows = await ExecuteAsync(
                sql,
                cancellationToken,
                ("id", subcategory.Id),
                ("tenant", subcategory.TenantId),
                ("name", subcategory.Name),
                ("description", subcategory.Description),
                ("active", subcategory.IsActive),
                ("at", subcategory.UpdatedAtUtc),
                ("by", subcategory.UpdatedBy),
                ("expected", expectedRowVersion));
            return rows == 1;
        }
        catch (PostgresException ex) when (ex.SqlState == UniqueViolation)
        {
            throw NameTaken("name", SubcategoryNameMessage);
        }
    }

    public async Task<bool> SoftDeleteSubcategoryAsync(
        Guid tenantId,
        Guid id,
        long expectedRowVersion,
        Guid? deletedBy,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = "UPDATE subcategories SET deleted_at = @at, deleted_by = @by, "
            + "row_version = row_version + 1, updated_at = @at, updated_by = @by "
            + "WHERE id = @id AND tenant_id = @tenant AND deleted_at IS NULL AND row_version = @expected";

        var rows = await ExecuteAsync(
            sql,
            cancellationToken,
            ("id", id),
            ("tenant", tenantId),
            ("at", deletedAtUtc),
            ("by", deletedBy),
            ("expected", expectedRowVersion));
        return rows == 1;
    }

    private static EdmException NameTaken(string field, string message)
    {
        var fieldErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        fieldErrors[field] = new[] { message };
        return new EdmException(EdmErrorCodes.ValidationFailed, fieldErrors);
    }

    private static Category ReadCategory(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetBoolean(4),
            reader.GetInt64(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.IsDBNull(7) ? null : reader.GetGuid(7));

    private static Subcategory ReadSubcategory(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetBoolean(5),
            reader.GetInt64(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            reader.IsDBNull(8) ? null : reader.GetGuid(8));

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
