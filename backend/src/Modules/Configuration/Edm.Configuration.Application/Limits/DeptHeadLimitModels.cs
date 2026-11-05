using Edm.Configuration.Domain.Limits;

namespace Edm.Configuration.Application.Limits;

/// <summary>
/// Input for saving a department-head limit. Nature is the code OPEX or CAPEX. RowVersion is the version of the limit
/// that is active now (null when none is active), so a save over a changed limit is refused (EDM-CONC-001).
/// </summary>
public sealed record DeptHeadLimitSave(
    string? Nature,
    decimal? AmountPkr,
    string? Description,
    DateOnly? EffectiveDate,
    long? RowVersion);

/// <summary>The values an audit event records for a limit row.</summary>
public sealed record DeptHeadLimitSnapshot(
    string Nature,
    decimal AmountPkr,
    string? Description,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    bool IsActive);

/// <summary>The limit summary for one nature: what applies now, and every past row.</summary>
public sealed record DeptHeadLimitSummary(
    LimitNature Nature,
    DeptHeadLimitStatus Status,
    IReadOnlyList<DeptHeadLimit> History);

/// <summary>
/// Persistence port for department-head limits. Every method filters by tenant. Each nature has its own table.
/// </summary>
public interface IDeptHeadLimitStore
{
    /// <summary>All rows for the nature, newest effective start first.</summary>
    Task<IReadOnlyList<DeptHeadLimit>> ListAsync(Guid tenantId, LimitNature nature, CancellationToken cancellationToken);

    /// <summary>
    /// In one transaction: close the active row (when there is one, if its version still matches
    /// <paramref name="expectedClosingVersion"/>) and insert the new active row. Returns false when the version does
    /// not match or a concurrent save already created an active row.
    /// </summary>
    Task<bool> ReplaceAsync(
        Guid tenantId,
        LimitNature nature,
        DeptHeadLimit? closing,
        long? expectedClosingVersion,
        DeptHeadLimit created,
        CancellationToken cancellationToken);

    /// <summary>Closes the active row if its version still matches. Returns false when it does not.</summary>
    Task<bool> DeactivateAsync(
        Guid tenantId,
        LimitNature nature,
        DeptHeadLimit closed,
        long expectedVersion,
        CancellationToken cancellationToken);
}
