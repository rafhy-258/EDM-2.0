using Edm.Configuration.Domain.Currencies;

namespace Edm.Configuration.Application.Currencies;

public interface ICurrencyStore
{
    Task EnsureBaseCurrencyAsync(Guid tenantId, DateTimeOffset seededAtUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<CurrencyView>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken cancellationToken);

    Task<CurrencyView?> FindAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<bool> CodeTakenAsync(Guid tenantId, string code, CancellationToken cancellationToken);

    Task InsertAsync(Currency currency, CurrencyRate initialRate, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(Currency currency, long expectedRowVersion, CancellationToken cancellationToken);

    Task<bool> SetRateAsync(
        CurrencyRate rate,
        long expectedCurrencyRowVersion,
        DateTimeOffset changedAtUtc,
        Guid? changedBy,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CurrencyRate>> ListRatesAsync(Guid tenantId, Guid currencyId, CancellationToken cancellationToken);

    Task<bool> HasRateHistoryAsync(Guid tenantId, Guid currencyId, CancellationToken cancellationToken);

    Task<bool> SoftDeleteAsync(
        Guid tenantId,
        Guid currencyId,
        long expectedRowVersion,
        Guid? deletedBy,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken);
}
