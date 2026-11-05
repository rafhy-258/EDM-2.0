namespace Edm.Configuration.Domain.Limits;

/// <summary>OPEX and CAPEX each have their own department-head limit (Part 5.5, open question 6).</summary>
public enum LimitNature
{
    Opex,
    Capex,
}

/// <summary>The stored codes for a nature, as used in the API and the audit log.</summary>
public static class LimitNatureCodes
{
    public static string Code(LimitNature nature) => nature == LimitNature.Opex ? "OPEX" : "CAPEX";

    public static bool TryParse(string? code, out LimitNature nature)
    {
        switch (code?.Trim().ToUpperInvariant())
        {
            case "OPEX":
                nature = LimitNature.Opex;
                return true;
            case "CAPEX":
                nature = LimitNature.Capex;
                return true;
            default:
                nature = default;
                return false;
        }
    }
}

/// <summary>
/// One row of a department-head limit (data-model design note section 3.1). Rows are never edited in place except to
/// close them. The row with IsActive set is the limit going forward. History is every row.
/// </summary>
public sealed record DeptHeadLimit(
    Guid Id,
    Guid TenantId,
    LimitNature Nature,
    decimal AmountPkr,
    string? Description,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    bool IsActive,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy);

/// <summary>Department-head limit rules (Master Prompt 4.2 and 5.5, open questions 1A-1 and 1A-2).</summary>
public static class DeptHeadLimitRules
{
    /// <summary>Used until an administrator creates an active limit (4.2, decision 1A-1).</summary>
    public const decimal DefaultAmountPkr = 20000m;

    public const int DescriptionMaxLength = 500;

    /// <summary>The largest value numeric(18,2) can hold.</summary>
    public const decimal MaximumAmountPkr = 9_999_999_999_999_999.99m;

    /// <summary>Asia/Karachi has no daylight saving time, so a fixed offset is exact.</summary>
    public static readonly TimeSpan KarachiOffset = TimeSpan.FromHours(5);

    public static string? AmountError(decimal? amount)
    {
        if (amount is null)
        {
            return "Enter an amount.";
        }

        var value = amount.Value;
        if (value <= 0)
        {
            return "The limit must be greater than zero.";
        }

        if (decimal.Round(value, 2) != value)
        {
            return "Use at most two decimal places.";
        }

        return value > MaximumAmountPkr ? "The limit is too large." : null;
    }

    public static string? DescriptionError(string? description)
        => description is not null && description.Trim().Length > DescriptionMaxLength
            ? $"The description must be {DescriptionMaxLength} characters or fewer."
            : null;

    /// <summary>The stored form: trimmed, and null when empty.</summary>
    public static string? NormaliseDescription(string? description)
    {
        var trimmed = description?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>The instant an effective date starts: midnight in Karachi, stored in UTC.</summary>
    public static DateTimeOffset StartOfKarachiDay(DateOnly date)
        => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), KarachiOffset).ToUniversalTime();

    /// <summary>The calendar date in Karachi of an instant.</summary>
    public static DateOnly KarachiDate(DateTimeOffset instant)
        => DateOnly.FromDateTime(instant.ToOffset(KarachiOffset).DateTime);
}

/// <summary>What applies at a moment in time, and how the limit is shown on the screen.</summary>
public sealed record DeptHeadLimitStatus(
    decimal CurrentAmountPkr,
    bool IsDefault,
    string Status,
    DeptHeadLimit? Open,
    DateTimeOffset? LastChangedAtUtc);

public static class DeptHeadLimitTimeline
{
    public const string StatusActive = "Active";
    public const string StatusScheduled = "Scheduled";
    public const string StatusDefault = "Default";

    /// <summary>
    /// A row is in effect at a moment when that moment is on or after its start and before its end (an open end
    /// means no end). If more than one row is in effect, the latest start wins. With no row in effect, the seeded
    /// default applies.
    /// </summary>
    public static DeptHeadLimitStatus Summarise(IReadOnlyList<DeptHeadLimit> rows, DateTimeOffset now)
    {
        var inEffect = rows
            .Where(r => r.EffectiveFrom <= now && (r.EffectiveTo is null || now < r.EffectiveTo.Value))
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefault();

        var open = rows.FirstOrDefault(r => r.IsActive);
        var status = open is null
            ? StatusDefault
            : open.EffectiveFrom <= now ? StatusActive : StatusScheduled;

        var lastChanged = rows.Count == 0 ? (DateTimeOffset?)null : rows.Max(r => r.UpdatedAtUtc);

        return new DeptHeadLimitStatus(
            inEffect?.AmountPkr ?? DeptHeadLimitRules.DefaultAmountPkr,
            inEffect is null,
            status,
            open,
            lastChanged);
    }
}
