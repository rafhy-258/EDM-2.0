using Edm.Audit.Application;
using Edm.Configuration.Application.Limits;
using Edm.Configuration.Domain.Limits;
using Edm.SharedKernel.Context;
using Edm.SharedKernel.Errors;
using Edm.UnitTests.Support;

namespace Edm.UnitTests.Configuration;

public sealed class DeptHeadLimitServiceTests
{
    private static readonly Guid Tenant = CategoryServiceTests.Tenant;
    private static readonly Guid Actor = CategoryServiceTests.Actor;

    /// <summary>5 Nov 2026, 09:00 in Karachi. Tomorrow is 6 Nov.</summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 4, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 5);
    private static readonly DateOnly Tomorrow = new(2026, 11, 6);

    private sealed class Rig
    {
        public Rig(DateTimeOffset? now = null)
        {
            var clock = new FixedClock(now ?? Now);
            var writer = new AuditWriter(
                Audit,
                clock,
                new FixedTenantContext(Tenant),
                new FixedActor(Actor, "Test Admin", "Administration", "EDM_ADMINISTRATOR"),
                new FixedCorrelation("corr-limits"));
            Service = new DeptHeadLimitService(
                Store,
                writer,
                new FixedTenantContext(Tenant),
                new FixedActor(Actor, "Test Admin", "Administration"),
                clock);
        }

        public InMemoryDeptHeadLimitStore Store { get; } = new();

        public FakeAuditStore Audit { get; } = new();

        public DeptHeadLimitService Service { get; }
    }

    private static DeptHeadLimitSave Save(decimal amount, DateOnly? date, long? rowVersion = null, string? nature = "OPEX", string? description = null)
        => new(nature, amount, description, date, rowVersion);

    [Fact]
    public async Task With_no_limit_the_seeded_default_applies_and_nothing_is_stored()
    {
        var rig = new Rig();

        var summary = await rig.Service.GetSummaryAsync("OPEX", default);

        Assert.True(summary.Status.IsDefault);
        Assert.Equal(20000m, summary.Status.CurrentAmountPkr);
        Assert.Equal(DeptHeadLimitTimeline.StatusDefault, summary.Status.Status);
        Assert.Null(summary.Status.Open);
        Assert.Empty(summary.History);
    }

    [Fact]
    public async Task A_limit_dated_today_applies_at_once()
    {
        var rig = new Rig();

        await rig.Service.SaveAsync(Save(50000m, Today), default);
        var summary = await rig.Service.GetSummaryAsync("OPEX", default);

        Assert.False(summary.Status.IsDefault);
        Assert.Equal(50000m, summary.Status.CurrentAmountPkr);
        Assert.Equal(DeptHeadLimitTimeline.StatusActive, summary.Status.Status);
    }

    [Fact]
    public async Task A_limit_dated_later_is_scheduled_and_the_default_still_applies()
    {
        var rig = new Rig();

        await rig.Service.SaveAsync(Save(50000m, Tomorrow), default);
        var summary = await rig.Service.GetSummaryAsync("OPEX", default);

        Assert.Equal(DeptHeadLimitTimeline.StatusScheduled, summary.Status.Status);
        Assert.Equal(20000m, summary.Status.CurrentAmountPkr);
        Assert.True(summary.Status.IsDefault);
    }

    [Fact]
    public async Task The_effective_date_starts_at_midnight_in_karachi_stored_in_utc()
    {
        var rig = new Rig();

        var created = await rig.Service.SaveAsync(Save(50000m, Tomorrow), default);

        Assert.Equal(new DateTimeOffset(2026, 11, 5, 19, 0, 0, TimeSpan.Zero), created.EffectiveFrom);
        Assert.Null(created.EffectiveTo);
        Assert.True(created.IsActive);
        Assert.Equal(1, created.RowVersion);
        Assert.Equal(DeptHeadLimitNatureTests.Opex, created.Nature);
    }

    [Fact]
    public async Task Saving_a_new_limit_ends_the_active_one_and_keeps_it_as_history()
    {
        var rig = new Rig();
        var first = await rig.Service.SaveAsync(Save(50000m, Today), default);

        var second = await rig.Service.SaveAsync(Save(75000m, Tomorrow, rowVersion: first.RowVersion), default);

        var summary = await rig.Service.GetSummaryAsync("OPEX", default);
        Assert.Equal(2, summary.History.Count);
        Assert.Equal(second.Id, summary.Status.Open!.Id);

        var closed = summary.History.Single(r => r.Id == first.Id);
        Assert.False(closed.IsActive);
        Assert.Equal(second.EffectiveFrom, closed.EffectiveTo!.Value);
        Assert.Equal(2, closed.RowVersion);

        Assert.Equal(new[] { "Created", "Replaced", "Created" }, rig.Audit.Events.Select(e => e.Action).ToArray());
    }

    [Fact]
    public async Task The_audit_events_carry_the_old_and_new_values()
    {
        var rig = new Rig();
        var first = await rig.Service.SaveAsync(Save(50000m, Today, description: "  Department heads  "), default);

        await rig.Service.SaveAsync(Save(75000m, Tomorrow, rowVersion: first.RowVersion), default);

        var replaced = rig.Audit.Events.Single(e => e.Action == "Replaced");
        Assert.Equal("Configuration", replaced.Module);
        Assert.Equal("DeptHeadLimit", replaced.EntityType);
        Assert.Equal(first.Id.ToString(), replaced.EntityId);
        Assert.Contains("\"amountPkr\":50000", replaced.BeforeJson);
        Assert.Contains("\"isActive\":true", replaced.BeforeJson);
        Assert.Contains("\"isActive\":false", replaced.AfterJson);
        Assert.Contains("\"description\":\"Department heads\"", replaced.BeforeJson);
    }

    [Fact]
    public async Task A_stale_version_is_refused_and_nothing_changes()
    {
        var rig = new Rig();
        var first = await rig.Service.SaveAsync(Save(50000m, Today), default);
        await rig.Service.SaveAsync(Save(75000m, Tomorrow, rowVersion: first.RowVersion), default);

        // The open row is version 3. The earlier rows were 1 (closed) and 2 (closed). A client holding 1 must not match it.
        Assert.Equal(3, rig.Store.Rows(DeptHeadLimitNatureTests.Opex).Single(r => r.IsActive).RowVersion);

        var error = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Service.SaveAsync(Save(90000m, Tomorrow, rowVersion: first.RowVersion), default));

        Assert.Equal(EdmErrorCodes.StaleUpdate, error.Error);
        Assert.Equal(2, rig.Store.Rows(DeptHeadLimitNatureTests.Opex).Count);
    }

    [Fact]
    public async Task A_version_is_refused_when_no_limit_is_active()
    {
        var rig = new Rig();

        var error = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Service.SaveAsync(Save(50000m, Today, rowVersion: 3), default));

        Assert.Equal(EdmErrorCodes.StaleUpdate, error.Error);
        Assert.Empty(rig.Store.Rows(DeptHeadLimitNatureTests.Opex));
    }

    [Fact]
    public async Task A_missing_version_is_refused_when_a_limit_is_active()
    {
        var rig = new Rig();
        await rig.Service.SaveAsync(Save(50000m, Today), default);

        var error = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Service.SaveAsync(Save(60000m, Tomorrow, rowVersion: null), default));

        Assert.Equal(EdmErrorCodes.StaleUpdate, error.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Zero_or_negative_amounts_are_field_errors_on_the_amount(decimal amount)
    {
        var rig = new Rig();

        var error = await Assert.ThrowsAsync<EdmException>(() => rig.Service.SaveAsync(Save(amount, Today), default));

        Assert.Equal(EdmErrorCodes.ValidationFailed, error.Error);
        Assert.Equal("The limit must be greater than zero.", error.FieldErrors["amountPkr"][0]);
    }

    [Fact]
    public async Task An_amount_with_three_decimals_is_refused()
    {
        var rig = new Rig();

        var error = await Assert.ThrowsAsync<EdmException>(() => rig.Service.SaveAsync(Save(50000.005m, Today), default));

        Assert.Equal("Use at most two decimal places.", error.FieldErrors["amountPkr"][0]);
    }

    [Fact]
    public async Task A_past_effective_date_is_refused()
    {
        var rig = new Rig();

        var error = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Service.SaveAsync(Save(50000m, Today.AddDays(-1)), default));

        Assert.Equal("The effective date cannot be in the past.", error.FieldErrors["effectiveDate"][0]);
    }

    [Fact]
    public async Task A_missing_effective_date_is_refused()
    {
        var rig = new Rig();

        var error = await Assert.ThrowsAsync<EdmException>(() => rig.Service.SaveAsync(Save(50000m, null), default));

        Assert.Equal("Choose an effective date.", error.FieldErrors["effectiveDate"][0]);
    }

    [Fact]
    public async Task An_effective_date_before_the_current_limit_start_is_refused()
    {
        var rig = new Rig();
        await rig.Service.SaveAsync(Save(50000m, Tomorrow), default);

        var error = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Service.SaveAsync(Save(60000m, Today, rowVersion: 1), default));

        Assert.Equal(EdmErrorCodes.ValidationFailed, error.Error);
        Assert.StartsWith("The effective date cannot be earlier than the current limit's start date", error.FieldErrors["effectiveDate"][0]);
    }

    [Fact]
    public async Task An_unknown_nature_is_refused()
    {
        var rig = new Rig();

        var error = await Assert.ThrowsAsync<EdmException>(() => rig.Service.SaveAsync(Save(50000m, Today, nature: "PAYROLL"), default));

        Assert.Equal(EdmErrorCodes.ValidationFailed, error.Error);
        Assert.True(error.FieldErrors.ContainsKey("nature"));
    }

    [Fact]
    public async Task A_description_is_trimmed_and_blank_becomes_null()
    {
        var rig = new Rig();

        var created = await rig.Service.SaveAsync(Save(50000m, Today, description: "   "), default);

        Assert.Null(created.Description);
    }

    [Fact]
    public async Task Deactivating_ends_the_limit_now_and_the_default_applies_again()
    {
        var rig = new Rig();
        var first = await rig.Service.SaveAsync(Save(50000m, Today), default);

        var deactivated = await rig.Service.DeactivateAsync("OPEX", first.RowVersion, default);
        var summary = await rig.Service.GetSummaryAsync("OPEX", default);

        Assert.True(deactivated);
        Assert.True(summary.Status.IsDefault);
        Assert.Equal(20000m, summary.Status.CurrentAmountPkr);
        Assert.Equal(DeptHeadLimitTimeline.StatusDefault, summary.Status.Status);
        var row = Assert.Single(summary.History);
        Assert.False(row.IsActive);
        Assert.Equal(Now, row.EffectiveTo!.Value);
        Assert.Equal("Deactivated", rig.Audit.Events.Last().Action);
    }

    [Fact]
    public async Task Deactivating_a_scheduled_limit_ends_it_at_its_own_start()
    {
        var rig = new Rig();
        var scheduled = await rig.Service.SaveAsync(Save(50000m, Tomorrow), default);

        await rig.Service.DeactivateAsync("OPEX", scheduled.RowVersion, default);

        var row = Assert.Single(rig.Store.Rows(DeptHeadLimitNatureTests.Opex));
        Assert.Equal(scheduled.EffectiveFrom, row.EffectiveTo!.Value);
    }

    [Fact]
    public async Task Deactivating_with_no_active_limit_returns_false()
    {
        var rig = new Rig();

        var result = await rig.Service.DeactivateAsync("OPEX", 1, default);

        Assert.False(result);
    }

    [Fact]
    public async Task Deactivating_with_a_stale_version_is_refused()
    {
        var rig = new Rig();
        await rig.Service.SaveAsync(Save(50000m, Today), default);

        var error = await Assert.ThrowsAsync<EdmException>(() => rig.Service.DeactivateAsync("OPEX", 9, default));

        Assert.Equal(EdmErrorCodes.StaleUpdate, error.Error);
        Assert.True(rig.Store.Rows(DeptHeadLimitNatureTests.Opex).Single().IsActive);
    }

    [Fact]
    public async Task OPEX_and_CAPEX_limits_are_independent()
    {
        var rig = new Rig();
        await rig.Service.SaveAsync(Save(50000m, Today, nature: "OPEX"), default);

        var capex = await rig.Service.GetSummaryAsync("CAPEX", default);
        await rig.Service.SaveAsync(Save(80000m, Today, nature: "CAPEX"), default);
        var opex = await rig.Service.GetSummaryAsync("OPEX", default);

        Assert.True(capex.Status.IsDefault);
        Assert.Equal(50000m, opex.Status.CurrentAmountPkr);
        Assert.Equal(80000m, (await rig.Service.GetSummaryAsync("CAPEX", default)).Status.CurrentAmountPkr);
    }
}

/// <summary>Nature constants for the tests, so the store lookups read clearly.</summary>
internal static class DeptHeadLimitNatureTests
{
    public const LimitNature Opex = LimitNature.Opex;
}

/// <summary>
/// In-memory limit store. It enforces what the PostgreSQL store enforces: one active row per tenant and nature, and
/// a version check on every change.
/// </summary>
internal sealed class InMemoryDeptHeadLimitStore : IDeptHeadLimitStore
{
    private readonly Dictionary<LimitNature, List<DeptHeadLimit>> _rows = new()
    {
        [LimitNature.Opex] = new List<DeptHeadLimit>(),
        [LimitNature.Capex] = new List<DeptHeadLimit>(),
    };

    public IReadOnlyList<DeptHeadLimit> Rows(LimitNature nature) => _rows[nature];

    public Task<IReadOnlyList<DeptHeadLimit>> ListAsync(Guid tenantId, LimitNature nature, CancellationToken cancellationToken)
    {
        IReadOnlyList<DeptHeadLimit> result = _rows[nature]
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.EffectiveFrom)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<bool> ReplaceAsync(
        Guid tenantId,
        LimitNature nature,
        DeptHeadLimit? closing,
        long? expectedClosingVersion,
        DeptHeadLimit created,
        CancellationToken cancellationToken)
    {
        var table = _rows[nature];

        if (closing is not null)
        {
            var index = table.FindIndex(r => r.Id == closing.Id && r.IsActive && r.RowVersion == expectedClosingVersion);
            if (index < 0)
            {
                return Task.FromResult(false);
            }

            table[index] = closing;
        }

        if (table.Any(r => r.IsActive))
        {
            return Task.FromResult(false);
        }

        table.Add(created);
        return Task.FromResult(true);
    }

    public Task<bool> DeactivateAsync(
        Guid tenantId,
        LimitNature nature,
        DeptHeadLimit closed,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var table = _rows[nature];
        var index = table.FindIndex(r => r.Id == closed.Id && r.IsActive && r.RowVersion == expectedVersion);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        table[index] = closed;
        return Task.FromResult(true);
    }
}
