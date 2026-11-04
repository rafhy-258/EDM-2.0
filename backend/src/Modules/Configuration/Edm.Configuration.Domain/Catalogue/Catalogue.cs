namespace Edm.Configuration.Domain.Catalogue;

/// <summary>
/// A category (Master Prompt Part 5.1). Immutable: a change creates a new value with a higher row version.
/// Deleted rows are not represented here; the store hides them.
/// </summary>
public sealed record Category(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Description,
    bool IsActive,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy);

/// <summary>A subcategory, always under one category (data-model design note section 2.4).</summary>
public sealed record Subcategory(
    Guid Id,
    Guid TenantId,
    Guid CategoryId,
    string Name,
    string? Description,
    bool IsActive,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy);
