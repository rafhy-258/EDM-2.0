using Edm.Configuration.Domain.Catalogue;

namespace Edm.Configuration.Application.Catalogue;

/// <summary>
/// Persistence port for categories and subcategories. Every method filters by tenant (ADR-0004).
/// Deleted rows are never returned. Updates and deletes succeed only when the row version still matches.
/// </summary>
public interface ICatalogueStore
{
    Task<IReadOnlyList<CategoryView>> ListCategoriesAsync(Guid tenantId, bool includeInactive, CancellationToken cancellationToken);

    Task<Category?> FindCategoryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>True when an active category with this name exists, ignoring the row <paramref name="exceptId"/>.</summary>
    Task<bool> CategoryNameTakenAsync(Guid tenantId, string name, Guid exceptId, CancellationToken cancellationToken);

    /// <summary>Count of subcategories that are not deleted, active or not.</summary>
    Task<int> CountSubcategoriesAsync(Guid tenantId, Guid categoryId, CancellationToken cancellationToken);

    Task InsertCategoryAsync(Category category, CancellationToken cancellationToken);

    /// <summary>Returns false when no row matches the id and the expected row version.</summary>
    Task<bool> UpdateCategoryAsync(Category category, long expectedRowVersion, CancellationToken cancellationToken);

    /// <summary>Returns false when no row matches the id and the expected row version.</summary>
    Task<bool> SoftDeleteCategoryAsync(
        Guid tenantId,
        Guid id,
        long expectedRowVersion,
        Guid? deletedBy,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SubcategoryView>> ListSubcategoriesAsync(
        Guid tenantId,
        Guid? categoryId,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<Subcategory?> FindSubcategoryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>True when an active subcategory with this name exists under the category, ignoring <paramref name="exceptId"/>.</summary>
    Task<bool> SubcategoryNameTakenAsync(
        Guid tenantId,
        Guid categoryId,
        string name,
        Guid exceptId,
        CancellationToken cancellationToken);

    Task InsertSubcategoryAsync(Subcategory subcategory, CancellationToken cancellationToken);

    /// <summary>Returns false when no row matches the id and the expected row version.</summary>
    Task<bool> UpdateSubcategoryAsync(Subcategory subcategory, long expectedRowVersion, CancellationToken cancellationToken);

    /// <summary>Returns false when no row matches the id and the expected row version.</summary>
    Task<bool> SoftDeleteSubcategoryAsync(
        Guid tenantId,
        Guid id,
        long expectedRowVersion,
        Guid? deletedBy,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken);
}
