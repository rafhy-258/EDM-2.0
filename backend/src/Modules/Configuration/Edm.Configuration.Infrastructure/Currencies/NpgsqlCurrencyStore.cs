using Edm.Configuration.Application.Currencies;
using Edm.Configuration.Domain.Currencies;
using Edm.SharedKernel.Errors;
using Npgsql;

namespace Edm.Configuration.Infrastructure.Currencies;

/// <summary>PostgreSQL store for tenant-scoped currencies and immutable rate history.</summary>
public sealed class NpgsqlCurrencyStore(string connectionString) : ICurrencyStore
{
    private const string UniqueViolation = PostgresErrorCodes.UniqueViolation;
    private const string CurrencyCodeConstraint = "currencies_tenant_code_unique";
    private const string CurrencyCodeMessage = "A currency with this code already exists.";
    private const string Columns = "c.id, c.tenant_id, c.code, c.name, c.is_base, c.is_active, c.row_version, "
        + "c.updated_at, c.updated_by, r.id, r.tenant_id, r.currency_id, r.rate, r.effective_from, r.effective_to, "
        + "r.is_active, r.row_version, r.updated_at, r.updated_by";

    public async Task EnsureBaseCurrencyAsync(Guid tenantId, DateTimeOffset seededAtUtc, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string currencySql = "INSERT INTO currencies "
            + "(id, tenant_id, code, name, is_base, is_active, row_version, created_at, updated_at) "
            + "VALUES (@id, @tenant, 'PKR', 'Pakistani Rupee', true, true, 1, @at, @at) "
            + "ON CONFLICT (tenant_id, code) WHERE deleted_at IS NULL DO NOTHING";
        await using (var command = new NpgsqlCommand(currencySql, connection, transaction))
        {
            Add(command, ("id", Guid.CreateVersion7()), ("tenant", tenantId), ("at", seededAtUtc));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        const string rateSql = "INSERT INTO currency_rates "
            + "(id, tenant_id, currency_id, rate, effective_from, is_active, row_version, created_at, updated_at) "
            + "SELECT @rate_id, @tenant, c.id, 1.0000, @at, true, 1, @at, @at FROM currencies c "
            + "WHERE c.tenant_id = @tenant AND c.code = 'PKR' AND c.is_base AND c.is_active AND c.deleted_at IS NULL "
            + "AND NOT EXISTS (SELECT 1 FROM currency_rates r WHERE r.tenant_id = @tenant AND r.currency_id = c.id AND r.is_active) "
            + "ON CONFLICT (tenant_id, currency_id) WHERE is_active DO NOTHING";
        await using (var command = new NpgsqlCommand(rateSql, connection, transaction))
        {
            Add(command, ("rate_id", Guid.CreateVersion7()), ("tenant", tenantId), ("at", seededAtUtc));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CurrencyView>> ListAsync(
        Guid tenantId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT " + Columns + " FROM currencies c "
            + "LEFT JOIN currency_rates r ON r.currency_id = c.id AND r.tenant_id = c.tenant_id AND r.is_active "
            + "WHERE c.tenant_id = @tenant AND c.deleted_at IS NULL AND (@include_inactive OR c.is_active) "
            + "ORDER BY c.code";

        return await QueryAsync(
            sql,
            ReadView,
            cancellationToken,
            ("tenant", tenantId),
            ("include_inactive", includeInactive));
    }

    public async Task<CurrencyView?> FindAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT " + Columns + " FROM currencies c "
            + "LEFT JOIN currency_rates r ON r.currency_id = c.id AND r.tenant_id = c.tenant_id AND r.is_active "
            + "WHERE c.tenant_id = @tenant AND c.id = @id AND c.deleted_at IS NULL";
        var rows = await QueryAsync(sql, ReadView, cancellationToken, ("tenant", tenantId), ("id", id));
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<bool> CodeTakenAsync(Guid tenantId, string code, CancellationToken cancellationToken)
    {
        const string sql = "SELECT EXISTS (SELECT 1 FROM currencies "
            + "WHERE tenant_id = @tenant AND deleted_at IS NULL AND code = @code)";
        var result = await ScalarAsync(sql, cancellationToken, ("tenant", tenantId), ("code", code));
        return (bool)result!;
    }

    public async Task InsertAsync(Currency currency, CurrencyRate initialRate, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            const string currencySql = "INSERT INTO currencies "
                + "(id, tenant_id, code, name, is_base, is_active, row_version, created_at, created_by, updated_at, updated_by) "
                + "VALUES (@id, @tenant, @code, @name, @base, @active, @version, @at, @by, @at, @by)";
            await using (var command = new NpgsqlCommand(currencySql, connection, transaction))
            {
                Add(command, ("id", currency.Id), ("tenant", currency.TenantId), ("code", currency.Code),
                    ("name", currency.Name), ("base", currency.IsBase), ("active", currency.IsActive),
                    ("version", currency.RowVersion), ("at", currency.UpdatedAtUtc), ("by", currency.UpdatedBy));
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            const string rateSql = "INSERT INTO currency_rates "
                + "(id, tenant_id, currency_id, rate, effective_from, effective_to, is_active, row_version, "
                + "created_at, created_by, updated_at, updated_by) "
                + "VALUES (@id, @tenant, @currency, @rate, @from, NULL, true, @version, @at, @by, @at, @by)";
            await using (var command = new NpgsqlCommand(rateSql, connection, transaction))
            {
                Add(command, ("id", initialRate.Id), ("tenant", initialRate.TenantId),
                    ("currency", initialRate.CurrencyId), ("rate", initialRate.Rate),
                    ("from", initialRate.EffectiveFrom), ("version", initialRate.RowVersion),
                    ("at", initialRate.UpdatedAtUtc), ("by", initialRate.UpdatedBy));
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException ex) when (
            ex.SqlState == UniqueViolation && ex.ConstraintName == CurrencyCodeConstraint)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw NameTaken();
        }
    }

    public async Task<bool> UpdateAsync(Currency currency, long expectedRowVersion, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE currencies SET name = @name, is_active = @active, "
            + "row_version = row_version + 1, updated_at = @at, updated_by = @by "
            + "WHERE id = @id AND tenant_id = @tenant AND deleted_at IS NULL AND row_version = @expected";
        var rows = await ExecuteAsync(
            sql,
            cancellationToken,
            ("name", currency.Name),
            ("active", currency.IsActive),
            ("at", currency.UpdatedAtUtc),
            ("by", currency.UpdatedBy),
            ("id", currency.Id),
            ("tenant", currency.TenantId),
            ("expected", expectedRowVersion));
        return rows == 1;
    }

    public async Task<bool> SetRateAsync(
        CurrencyRate rate,
        long expectedCurrencyRowVersion,
        DateTimeOffset changedAtUtc,
        Guid? changedBy,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string updateCurrency = "UPDATE currencies SET row_version = row_version + 1, updated_at = @at, updated_by = @by "
            + "WHERE id = @id AND tenant_id = @tenant AND deleted_at IS NULL AND row_version = @expected";
        await using (var command = new NpgsqlCommand(updateCurrency, connection, transaction))
        {
            Add(command, ("at", changedAtUtc), ("by", changedBy), ("id", rate.CurrencyId),
                ("tenant", rate.TenantId), ("expected", expectedCurrencyRowVersion));
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
        }

        const string closeRate = "UPDATE currency_rates SET is_active = false, effective_to = @at, "
            + "row_version = row_version + 1, updated_at = @at, updated_by = @by "
            + "WHERE currency_id = @currency AND tenant_id = @tenant AND is_active";
        await using (var command = new NpgsqlCommand(closeRate, connection, transaction))
        {
            Add(command, ("at", changedAtUtc), ("by", changedBy), ("currency", rate.CurrencyId), ("tenant", rate.TenantId));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertRate = "INSERT INTO currency_rates "
            + "(id, tenant_id, currency_id, rate, effective_from, effective_to, is_active, row_version, "
            + "created_at, created_by, updated_at, updated_by) "
            + "VALUES (@id, @tenant, @currency, @rate, @from, NULL, true, @version, @at, @by, @at, @by)";
        await using (var command = new NpgsqlCommand(insertRate, connection, transaction))
        {
            Add(command, ("id", rate.Id), ("tenant", rate.TenantId), ("currency", rate.CurrencyId),
                ("rate", rate.Rate), ("from", rate.EffectiveFrom), ("version", rate.RowVersion),
                ("at", rate.UpdatedAtUtc), ("by", rate.UpdatedBy));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<CurrencyRate>> ListRatesAsync(
        Guid tenantId,
        Guid currencyId,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, tenant_id, currency_id, rate, effective_from, effective_to, is_active, "
            + "row_version, updated_at, updated_by FROM currency_rates "
            + "WHERE tenant_id = @tenant AND currency_id = @currency ORDER BY effective_from DESC, id";
        return await QueryAsync(sql, reader => ReadRate(reader), cancellationToken, ("tenant", tenantId), ("currency", currencyId));
    }

    public async Task<bool> HasRateHistoryAsync(Guid tenantId, Guid currencyId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT EXISTS (SELECT 1 FROM currency_rates WHERE tenant_id = @tenant AND currency_id = @currency)";
        var result = await ScalarAsync(sql, cancellationToken, ("tenant", tenantId), ("currency", currencyId));
        return (bool)result!;
    }

    public async Task<bool> SoftDeleteAsync(
        Guid tenantId,
        Guid currencyId,
        long expectedRowVersion,
        Guid? deletedBy,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = "UPDATE currencies SET deleted_at = @at, deleted_by = @by, "
            + "row_version = row_version + 1, updated_at = @at, updated_by = @by "
            + "WHERE id = @id AND tenant_id = @tenant AND deleted_at IS NULL AND row_version = @expected";
        var rows = await ExecuteAsync(
            sql,
            cancellationToken,
            ("at", deletedAtUtc),
            ("by", deletedBy),
            ("id", currencyId),
            ("tenant", tenantId),
            ("expected", expectedRowVersion));
        return rows == 1;
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<NpgsqlDataReader, T> read,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        Add(command, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(read(reader));
        }

        return rows;
    }

    private async Task<object?> ScalarAsync(
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        Add(command, parameters);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private async Task<int> ExecuteAsync(
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        Add(command, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void Add(NpgsqlCommand command, params (string Name, object? Value)[] parameters)
    {
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
    }

    private static CurrencyView ReadView(NpgsqlDataReader reader)
    {
        var currency = new Currency(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetBoolean(4),
            reader.GetBoolean(5),
            reader.GetInt64(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            reader.IsDBNull(8) ? null : reader.GetGuid(8));
        var rate = reader.IsDBNull(9) ? null : ReadRate(reader, 9);
        return new CurrencyView(currency, rate);
    }

    private static CurrencyRate ReadRate(NpgsqlDataReader reader, int offset = 0)
        => new(
            reader.GetGuid(offset),
            reader.GetGuid(offset + 1),
            reader.GetGuid(offset + 2),
            reader.GetDecimal(offset + 3),
            reader.GetFieldValue<DateTimeOffset>(offset + 4),
            reader.IsDBNull(offset + 5) ? null : reader.GetFieldValue<DateTimeOffset>(offset + 5),
            reader.GetBoolean(offset + 6),
            reader.GetInt64(offset + 7),
            reader.GetFieldValue<DateTimeOffset>(offset + 8),
            reader.IsDBNull(offset + 9) ? null : reader.GetGuid(offset + 9));

    private static EdmException NameTaken()
        => new(EdmErrorCodes.ValidationFailed, new Dictionary<string, string[]> { ["code"] = [CurrencyCodeMessage] });
}
