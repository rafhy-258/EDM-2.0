using Edm.Audit.Application;
using Edm.Configuration.Application.Catalogue;
using Edm.Configuration.Domain.Currencies;
using Edm.SharedKernel.Context;
using Edm.SharedKernel.Errors;

namespace Edm.Configuration.Application.Currencies;

/// <summary>
/// Currency management (Master Prompt Part 5.6). Rate changes append a new effective-dated record and preserve history.
/// Policy versions and maker-checker are deferred to M1A-06.
/// </summary>
public sealed class CurrencyService(
    ICurrencyStore store,
    IConfigurationUsageChecker usages,
    IAuditWriter audit,
    ITenantContext tenant,
    IActorContext actor,
    IClock clock)
{
    private const string DuplicateCodeMessage = "A currency with this code already exists.";

    public Task<IReadOnlyList<CurrencyView>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
        => ListWithBaseAsync(includeInactive, cancellationToken);

    private async Task<IReadOnlyList<CurrencyView>> ListWithBaseAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        await store.EnsureBaseCurrencyAsync(tenant.TenantId, clock.UtcNow, cancellationToken);
        return await store.ListAsync(tenant.TenantId, includeInactive, cancellationToken);
    }

    public async Task<CurrencyView> CreateAsync(CurrencyCreate input, CancellationToken cancellationToken)
    {
        await store.EnsureBaseCurrencyAsync(tenant.TenantId, clock.UtcNow, cancellationToken);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        AddError(errors, "code", CurrencyRules.CodeError(input.Code));
        AddError(errors, "name", CurrencyRules.NameError(input.Name));
        AddError(errors, "rate", CurrencyRules.RateError(input.Rate));

        if (errors.Count == 0)
        {
            var code = CurrencyRules.NormaliseCode(input.Code!);
            if (await store.CodeTakenAsync(tenant.TenantId, code, cancellationToken))
            {
                AddError(errors, "code", DuplicateCodeMessage);
            }
        }

        ThrowIfInvalid(errors);
        CurrencyRules.TryParseRate(input.Rate, out var rateValue);

        var now = clock.UtcNow;
        var currency = new Currency(
            Guid.CreateVersion7(),
            tenant.TenantId,
            CurrencyRules.NormaliseCode(input.Code!),
            CurrencyRules.NormaliseName(input.Name!),
            IsBase: false,
            IsActive: true,
            RowVersion: 1,
            UpdatedAtUtc: now,
            UpdatedBy: actor.UserId);
        var rate = NewRate(currency.Id, rateValue, now);

        await store.InsertAsync(currency, rate, cancellationToken);
        await audit.WriteAsync(
            CatalogueAudit.Draft("Currency", currency.Id, "Created", before: null, after: Snapshot(currency, rate)),
            cancellationToken);
        return new CurrencyView(currency, rate);
    }

    public async Task<CurrencyView?> UpdateAsync(Guid id, CurrencyUpdate input, CancellationToken cancellationToken)
    {
        var existing = await store.FindAsync(tenant.TenantId, id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var nameError = CurrencyRules.NameError(input.Name);
        if (nameError is not null)
        {
            ThrowFieldError("name", nameError);
        }

        if (existing.Currency.RowVersion != input.RowVersion)
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        var updated = existing.Currency with
        {
            Name = CurrencyRules.NormaliseName(input.Name!),
            RowVersion = existing.Currency.RowVersion + 1,
            UpdatedAtUtc = clock.UtcNow,
            UpdatedBy = actor.UserId,
        };
        if (!await store.UpdateAsync(updated, input.RowVersion, cancellationToken))
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        var after = existing with { Currency = updated };
        await audit.WriteAsync(
            CatalogueAudit.Draft(
                "Currency",
                id,
                "Updated",
                Snapshot(existing.Currency, existing.ActiveRate),
                Snapshot(updated, existing.ActiveRate)),
            cancellationToken);
        return after;
    }

    public async Task<CurrencyRate?> SetRateAsync(Guid id, CurrencyRateChange input, CancellationToken cancellationToken)
    {
        var existing = await store.FindAsync(tenant.TenantId, id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        if (existing.Currency.IsBase)
        {
            ThrowFieldError("rate", "The PKR base rate is fixed at 1.");
        }

        var rateError = CurrencyRules.RateError(input.Rate);
        if (rateError is not null)
        {
            ThrowFieldError("rate", rateError);
        }

        if (existing.Currency.RowVersion != input.RowVersion)
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        CurrencyRules.TryParseRate(input.Rate, out var rateValue);
        var now = clock.UtcNow;
        var updatedRate = NewRate(id, rateValue, now);
        if (!await store.SetRateAsync(updatedRate, input.RowVersion, now, actor.UserId, cancellationToken))
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        await audit.WriteAsync(
            CatalogueAudit.Draft(
                "Currency",
                id,
                "RateChanged",
                Snapshot(existing.Currency, existing.ActiveRate),
                Snapshot(existing.Currency with { RowVersion = existing.Currency.RowVersion + 1 }, updatedRate)),
            cancellationToken);
        return updatedRate;
    }

    public async Task<CurrencyView?> SetActiveAsync(
        Guid id,
        bool isActive,
        long rowVersion,
        CancellationToken cancellationToken)
    {
        var existing = await store.FindAsync(tenant.TenantId, id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        if (existing.Currency.IsBase && !isActive)
        {
            ThrowFieldError("isActive", "PKR cannot be deactivated.");
        }

        if (existing.Currency.RowVersion != rowVersion)
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        if (existing.Currency.IsActive == isActive)
        {
            return existing;
        }

        var updated = existing.Currency with
        {
            IsActive = isActive,
            RowVersion = existing.Currency.RowVersion + 1,
            UpdatedAtUtc = clock.UtcNow,
            UpdatedBy = actor.UserId,
        };
        if (!await store.UpdateAsync(updated, rowVersion, cancellationToken))
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        await audit.WriteAsync(
            CatalogueAudit.Draft(
                "Currency",
                id,
                isActive ? "Reactivated" : "Deactivated",
                Snapshot(existing.Currency, existing.ActiveRate),
                Snapshot(updated, existing.ActiveRate)),
            cancellationToken);
        return existing with { Currency = updated };
    }

    public async Task<IReadOnlyList<CurrencyRate>?> ListRatesAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(tenant.TenantId, id, cancellationToken) is null)
        {
            return null;
        }

        return await store.ListRatesAsync(tenant.TenantId, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, long rowVersion, CancellationToken cancellationToken)
    {
        var existing = await store.FindAsync(tenant.TenantId, id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        if (existing.Currency.IsBase)
        {
            ThrowFieldError("currency", "PKR cannot be deleted.");
        }

        if (existing.Currency.RowVersion != rowVersion)
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        if (await store.HasRateHistoryAsync(tenant.TenantId, id, cancellationToken)
            || (await usages.FindUsagesAsync(ConfigurationEntity.Currency, tenant.TenantId, id, cancellationToken)).Count > 0)
        {
            throw new EdmException(EdmErrorCodes.RecordInUse);
        }

        if (!await store.SoftDeleteAsync(tenant.TenantId, id, rowVersion, actor.UserId, clock.UtcNow, cancellationToken))
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        await audit.WriteAsync(
            CatalogueAudit.Draft("Currency", id, "Deleted", Snapshot(existing.Currency, existing.ActiveRate), after: null),
            cancellationToken);
        return true;
    }

    private CurrencyRate NewRate(Guid currencyId, decimal rate, DateTimeOffset now)
        => new(
            Guid.CreateVersion7(),
            tenant.TenantId,
            currencyId,
            rate,
            now,
            EffectiveTo: null,
            IsActive: true,
            RowVersion: 1,
            UpdatedAtUtc: now,
            UpdatedBy: actor.UserId);

    private static CurrencySnapshot Snapshot(Currency currency, CurrencyRate? rate)
        => new(currency.Code, currency.Name, currency.IsActive, rate?.Rate);

    private static void AddError(Dictionary<string, string[]> errors, string field, string? message)
    {
        if (message is not null)
        {
            errors[field] = [message];
        }
    }

    private static void ThrowFieldError(string field, string message)
        => throw new EdmException(EdmErrorCodes.ValidationFailed, new Dictionary<string, string[]> { [field] = [message] });

    private static void ThrowIfInvalid(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw new EdmException(EdmErrorCodes.ValidationFailed, errors);
        }
    }
}

public sealed record CurrencyCreate(string? Code, string? Name, string? Rate);

public sealed record CurrencyUpdate(string? Name, long RowVersion);

public sealed record CurrencyRateChange(string? Rate, long RowVersion);

public sealed record CurrencySnapshot(string Code, string Name, bool IsActive, decimal? Rate);
