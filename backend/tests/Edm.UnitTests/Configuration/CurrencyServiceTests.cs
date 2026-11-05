using Edm.Audit.Application;
using Edm.Configuration.Application.Currencies;
using Edm.Configuration.Domain.Currencies;
using Edm.SharedKernel.Context;
using Edm.SharedKernel.Errors;
using Edm.UnitTests.Support;

namespace Edm.UnitTests.Configuration;

public sealed class CurrencyServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.Parse("5b0a1c2d-0000-4000-8000-000000000042");

    [Fact]
    public void Rules_accept_three_letter_codes_and_four_decimal_rates()
    {
        Assert.Null(CurrencyRules.CodeError(" usd "));
        Assert.Null(CurrencyRules.NameError("US Dollar"));
        Assert.Null(CurrencyRules.RateError("278.1234"));
        Assert.Equal("Enter a three-letter currency code, for example USD.", CurrencyRules.CodeError("US"));
        Assert.Equal("Use at most four decimal places.", CurrencyRules.RateError("1.00001"));
        Assert.Equal("The rate must be greater than zero.", CurrencyRules.RateError("0"));
    }

    [Fact]
    public async Task Create_normalises_the_code_and_records_the_initial_rate_and_audit_event()
    {
        var rig = new Rig();

        var created = await rig.Service.CreateAsync(new CurrencyCreate(" usd ", " US Dollar ", "278.1234"), default);

        Assert.Equal("USD", created.Currency.Code);
        Assert.Equal("US Dollar", created.Currency.Name);
        Assert.Equal(278.1234m, created.ActiveRate!.Rate);
        Assert.Contains(rig.Store.Rates, r => r.CurrencyId == rig.Store.BaseCurrency?.Id && r.IsActive && r.Rate == 1m);
        Assert.Single(rig.Store.Rates, r => r.CurrencyId == created.Currency.Id);
        var audit = Assert.Single(rig.Audit.Events);
        Assert.Equal("Currency", audit.EntityType);
        Assert.Equal(created.Currency.Id.ToString(), audit.EntityId);
        Assert.Equal("Created", audit.Action);
        Assert.Null(audit.BeforeJson);
        Assert.Contains("278.1234", audit.AfterJson);
    }

    [Fact]
    public async Task Duplicate_code_is_case_insensitive_and_rate_change_preserves_history()
    {
        var rig = new Rig();
        var currency = await rig.Service.CreateAsync(new CurrencyCreate("USD", "US Dollar", "278.1234"), default);

        var duplicate = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Service.CreateAsync(new CurrencyCreate("usd", "Dollar", "1"), default));
        Assert.Equal("A currency with this code already exists.", duplicate.FieldErrors["code"][0]);

        var changed = await rig.Service.SetRateAsync(
            currency.Currency.Id,
            new CurrencyRateChange("280.5", currency.Currency.RowVersion),
            default);

        Assert.Equal(2, rig.Store.Rates.Count(r => r.CurrencyId == currency.Currency.Id));
        Assert.Equal(280.5m, changed!.Rate);
        Assert.Equal(
            new[] { false, true },
            rig.Store.Rates.Where(r => r.CurrencyId == currency.Currency.Id).OrderBy(r => r.EffectiveFrom).Select(r => r.IsActive));
        Assert.Equal(Now, rig.Store.Rates.Where(r => r.CurrencyId == currency.Currency.Id).OrderBy(r => r.EffectiveFrom).First().EffectiveTo);
        Assert.Equal(new[] { "Created", "RateChanged" }, rig.Audit.Events.Select(e => e.Action));
        Assert.Contains("278.1234", rig.Audit.Events[1].BeforeJson);
        Assert.Contains("280.5", rig.Audit.Events[1].AfterJson);
    }

    [Fact]
    public async Task Invalid_rate_and_stale_rate_change_are_refused()
    {
        var rig = new Rig();
        var currency = await rig.Service.CreateAsync(new CurrencyCreate("USD", "US Dollar", "278"), default);

        var invalid = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Service.SetRateAsync(currency.Currency.Id, new CurrencyRateChange("1.12345", 1), default));
        Assert.Equal("Use at most four decimal places.", invalid.FieldErrors["rate"][0]);

        await rig.Service.UpdateAsync(currency.Currency.Id, new CurrencyUpdate("US dollar", 1), default);
        var stale = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Service.SetRateAsync(currency.Currency.Id, new CurrencyRateChange("280", 1), default));

        Assert.Equal(EdmErrorCodes.StaleUpdate, stale.Error);
        Assert.Single(rig.Store.Rates, r => r.CurrencyId == currency.Currency.Id);
    }

    [Fact]
    public async Task Currency_with_rate_history_cannot_be_deleted()
    {
        var rig = new Rig();
        var currency = await rig.Service.CreateAsync(new CurrencyCreate("USD", "US Dollar", "278"), default);

        var error = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Service.DeleteAsync(currency.Currency.Id, currency.Currency.RowVersion, default));

        Assert.Equal(EdmErrorCodes.RecordInUse, error.Error);
        Assert.DoesNotContain(rig.Audit.Events, e => e.Action == "Deleted");
    }

    [Fact]
    public async Task Base_currency_rate_and_active_state_are_protected()
    {
        var rig = new Rig(includeBase: true);
        var baseCurrency = rig.Store.BaseCurrency!;

        var rateError = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Service.SetRateAsync(baseCurrency.Id, new CurrencyRateChange("2", 1), default));
        var activeError = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Service.SetActiveAsync(baseCurrency.Id, false, 1, default));
        var deleteError = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Service.DeleteAsync(baseCurrency.Id, 1, default));

        Assert.Equal("The PKR base rate is fixed at 1.", rateError.FieldErrors["rate"][0]);
        Assert.Equal("PKR cannot be deactivated.", activeError.FieldErrors["isActive"][0]);
        Assert.Equal("PKR cannot be deleted.", deleteError.FieldErrors["currency"][0]);
        Assert.Empty(rig.Audit.Events);
    }

    private sealed class Rig
    {
        public Rig(bool includeBase = false)
        {
            var tenant = new FixedTenantContext(TenantDefaults.SingleTenant);
            var actor = new FixedActor(Actor, "Test Admin", "Administration");
            var clock = new FixedClock(Now);
            Store = new InMemoryCurrencyStore(includeBase);
            Audit = new FakeAuditStore();
            var usages = new FakeUsageChecker();
            var writer = new AuditWriter(
                Audit,
                clock,
                tenant,
                actor,
                new FixedCorrelation("corr-currency"));
            Service = new CurrencyService(Store, usages, writer, tenant, actor, clock);
        }

        public InMemoryCurrencyStore Store { get; }
        public FakeAuditStore Audit { get; }
        public CurrencyService Service { get; }
    }
}

internal sealed class InMemoryCurrencyStore : ICurrencyStore
{
    private readonly List<Currency> _currencies = [];
    private readonly List<CurrencyRate> _rates = [];
    private readonly HashSet<Guid> _deleted = [];

    public InMemoryCurrencyStore(bool includeBase)
    {
        if (!includeBase)
        {
            return;
        }

        var tenant = TenantDefaults.SingleTenant;
        var baseCurrency = new Currency(
            Guid.Parse("00000000-0000-7000-8000-000000000101"),
            tenant,
            "PKR",
            "Pakistani Rupee",
            true,
            true,
            1,
            new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero),
            null);
        BaseCurrency = baseCurrency;
        _currencies.Add(baseCurrency);
        _rates.Add(new CurrencyRate(
            Guid.Parse("00000000-0000-7000-8000-000000000102"),
            tenant,
            baseCurrency.Id,
            1m,
            baseCurrency.UpdatedAtUtc,
            null,
            true,
            1,
            baseCurrency.UpdatedAtUtc,
            null));
    }

    public Currency? BaseCurrency { get; private set; }
    public IReadOnlyList<CurrencyRate> Rates => _rates;

    public Task EnsureBaseCurrencyAsync(Guid tenantId, DateTimeOffset seededAtUtc, CancellationToken cancellationToken)
    {
        if (_currencies.Any(c => c.TenantId == tenantId && c.IsBase && !_deleted.Contains(c.Id)))
        {
            return Task.CompletedTask;
        }

        var currency = new Currency(
            Guid.CreateVersion7(),
            tenantId,
            "PKR",
            "Pakistani Rupee",
            true,
            true,
            1,
            seededAtUtc,
            null);
        BaseCurrency = currency;
        _currencies.Add(currency);
        _rates.Add(new CurrencyRate(
            Guid.CreateVersion7(),
            tenantId,
            currency.Id,
            1m,
            seededAtUtc,
            null,
            true,
            1,
            seededAtUtc,
            null));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CurrencyView>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CurrencyView>>(_currencies
            .Where(c => c.TenantId == tenantId && !_deleted.Contains(c.Id) && (includeInactive || c.IsActive))
            .Select(ToView)
            .ToArray());

    public Task<CurrencyView?> FindAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_currencies.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id && !_deleted.Contains(id)) is { } currency
            ? ToView(currency)
            : null);

    public Task<bool> CodeTakenAsync(Guid tenantId, string code, CancellationToken cancellationToken)
        => Task.FromResult(_currencies.Any(c =>
            c.TenantId == tenantId && !_deleted.Contains(c.Id) && c.Code == code));

    public Task InsertAsync(Currency currency, CurrencyRate initialRate, CancellationToken cancellationToken)
    {
        _currencies.Add(currency);
        _rates.Add(initialRate);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(Currency currency, long expectedRowVersion, CancellationToken cancellationToken)
    {
        var index = _currencies.FindIndex(c =>
            c.Id == currency.Id && c.TenantId == currency.TenantId && c.RowVersion == expectedRowVersion && !_deleted.Contains(c.Id));
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        _currencies[index] = currency;
        return Task.FromResult(true);
    }

    public Task<bool> SetRateAsync(
        CurrencyRate rate,
        long expectedCurrencyRowVersion,
        DateTimeOffset changedAtUtc,
        Guid? changedBy,
        CancellationToken cancellationToken)
    {
        var index = _currencies.FindIndex(c =>
            c.Id == rate.CurrencyId && c.TenantId == rate.TenantId
            && c.RowVersion == expectedCurrencyRowVersion && !_deleted.Contains(c.Id));
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        _currencies[index] = _currencies[index] with
        {
            RowVersion = expectedCurrencyRowVersion + 1,
            UpdatedAtUtc = changedAtUtc,
            UpdatedBy = changedBy,
        };
        for (var i = 0; i < _rates.Count; i++)
        {
            if (_rates[i].CurrencyId == rate.CurrencyId && _rates[i].TenantId == rate.TenantId && _rates[i].IsActive)
            {
                _rates[i] = _rates[i] with
                {
                    IsActive = false,
                    EffectiveTo = changedAtUtc,
                    RowVersion = _rates[i].RowVersion + 1,
                    UpdatedAtUtc = changedAtUtc,
                    UpdatedBy = changedBy,
                };
            }
        }

        _rates.Add(rate);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<CurrencyRate>> ListRatesAsync(Guid tenantId, Guid currencyId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CurrencyRate>>(_rates
            .Where(r => r.TenantId == tenantId && r.CurrencyId == currencyId)
            .OrderByDescending(r => r.EffectiveFrom)
            .ToArray());

    public Task<bool> HasRateHistoryAsync(Guid tenantId, Guid currencyId, CancellationToken cancellationToken)
        => Task.FromResult(_rates.Any(r => r.TenantId == tenantId && r.CurrencyId == currencyId));

    public Task<bool> SoftDeleteAsync(
        Guid tenantId,
        Guid currencyId,
        long expectedRowVersion,
        Guid? deletedBy,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken)
    {
        var currency = _currencies.FirstOrDefault(c => c.TenantId == tenantId && c.Id == currencyId && c.RowVersion == expectedRowVersion);
        if (currency is null || _deleted.Contains(currencyId))
        {
            return Task.FromResult(false);
        }

        _deleted.Add(currencyId);
        return Task.FromResult(true);
    }

    private CurrencyView ToView(Currency currency)
        => new(currency, _rates.FirstOrDefault(r => r.CurrencyId == currency.Id && r.TenantId == currency.TenantId && r.IsActive));
}
