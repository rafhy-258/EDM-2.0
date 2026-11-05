using Edm.Configuration.Domain.Limits;

namespace Edm.UnitTests.Configuration;

public sealed class DeptHeadLimitRulesTests
{
    private static readonly Guid Tenant = CategoryServiceTests.Tenant;
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_missing_amount_is_refused()
    {
        Assert.Equal("Enter an amount.", DeptHeadLimitRules.AmountError(null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void Zero_and_negative_amounts_are_refused(decimal amount)
    {
        Assert.Equal("The limit must be greater than zero.", DeptHeadLimitRules.AmountError(amount));
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(50000)]
    [InlineData(50000.5)]
    [InlineData(50000.25)]
    public void Positive_amounts_with_at_most_two_decimals_are_accepted(decimal amount)
    {
        Assert.Null(DeptHeadLimitRules.AmountError(amount));
    }

    [Fact]
    public void Three_decimals_are_refused()
    {
        Assert.Equal("Use at most two decimal places.", DeptHeadLimitRules.AmountError(50000.001m));
    }

    [Fact]
    public void An_amount_too_large_for_the_column_is_refused()
    {
        Assert.Equal("The limit is too large.", DeptHeadLimitRules.AmountError(10_000_000_000_000_000m));
    }

    [Fact]
    public void A_description_over_500_characters_is_refused()
    {
        Assert.Null(DeptHeadLimitRules.DescriptionError(new string('d', 500)));
        Assert.NotNull(DeptHeadLimitRules.DescriptionError(new string('d', 501)));
    }

    [Fact]
    public void The_start_of_an_effective_date_is_midnight_in_karachi()
    {
        var start = DeptHeadLimitRules.StartOfKarachiDay(new DateOnly(2026, 11, 6));

        Assert.Equal(new DateTimeOffset(2026, 11, 5, 19, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateOnly(2026, 11, 6), DeptHeadLimitRules.KarachiDate(start));
    }

    [Fact]
    public void A_row_is_in_effect_from_its_exact_start_instant()
    {
        var row = Row(amount: 50000m, from: Now, to: null, active: true);

        var status = DeptHeadLimitTimeline.Summarise([row], Now);

        Assert.False(status.IsDefault);
        Assert.Equal(50000m, status.CurrentAmountPkr);
        Assert.Equal(DeptHeadLimitTimeline.StatusActive, status.Status);
    }

    [Fact]
    public void A_scheduled_row_does_not_replace_the_one_in_effect_until_its_start()
    {
        var current = Row(amount: 50000m, from: Now.AddDays(-10), to: Now.AddDays(1), active: false);
        var scheduled = Row(amount: 75000m, from: Now.AddDays(1), to: null, active: true);

        var status = DeptHeadLimitTimeline.Summarise([scheduled, current], Now);

        Assert.Equal(50000m, status.CurrentAmountPkr);
        Assert.Equal(DeptHeadLimitTimeline.StatusScheduled, status.Status);
        Assert.Same(scheduled, status.Open);
    }

    [Fact]
    public void The_scheduled_row_takes_over_at_its_start()
    {
        var current = Row(amount: 50000m, from: Now.AddDays(-10), to: Now.AddDays(1), active: false);
        var scheduled = Row(amount: 75000m, from: Now.AddDays(1), to: null, active: true);

        var status = DeptHeadLimitTimeline.Summarise([scheduled, current], Now.AddDays(1));

        Assert.Equal(75000m, status.CurrentAmountPkr);
        Assert.Equal(DeptHeadLimitTimeline.StatusActive, status.Status);
    }

    [Fact]
    public void After_deactivation_the_default_applies()
    {
        var ended = Row(amount: 50000m, from: Now.AddDays(-10), to: Now.AddDays(-1), active: false);

        var status = DeptHeadLimitTimeline.Summarise([ended], Now);

        Assert.True(status.IsDefault);
        Assert.Equal(DeptHeadLimitRules.DefaultAmountPkr, status.CurrentAmountPkr);
        Assert.Equal(DeptHeadLimitTimeline.StatusDefault, status.Status);
        Assert.Null(status.Open);
    }

    [Fact]
    public void With_no_rows_the_status_is_default_and_nothing_was_changed()
    {
        var status = DeptHeadLimitTimeline.Summarise([], Now);

        Assert.Equal(20000m, status.CurrentAmountPkr);
        Assert.Null(status.LastChangedAtUtc);
    }

    [Fact]
    public void Nature_codes_parse_case_insensitively_and_reject_others()
    {
        Assert.True(LimitNatureCodes.TryParse(" opex ", out var opex));
        Assert.Equal(LimitNature.Opex, opex);
        Assert.True(LimitNatureCodes.TryParse("CAPEX", out var capex));
        Assert.Equal(LimitNature.Capex, capex);
        Assert.False(LimitNatureCodes.TryParse("PAYROLL", out _));
        Assert.False(LimitNatureCodes.TryParse(null, out _));
    }

    private static DeptHeadLimit Row(decimal amount, DateTimeOffset from, DateTimeOffset? to, bool active)
        => new(
            Guid.CreateVersion7(),
            Tenant,
            LimitNature.Opex,
            amount,
            null,
            from,
            to,
            active,
            RowVersion: 1,
            UpdatedAtUtc: from,
            UpdatedBy: null);
}
