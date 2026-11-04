using Edm.Configuration.Application.Catalogue;
using Edm.Configuration.Domain.Catalogue;

namespace Edm.UnitTests.Configuration;

/// <summary>
/// In-memory catalogue store for the service tests. It applies the same rules as the PostgreSQL store: soft delete,
/// case-insensitive uniqueness among active rows, and row-version checks on update and delete.
/// </summary>
internal sealed class InMemoryCatalogueStore : ICatalogueStore
{
    private readonly List<Category> _categories = [];
    private readonly List<Subcategory> _subcategories = [];
    private readonly HashSet<Guid> _deletedCategories = [];
    private readonly HashSet<Guid> _deletedSubcategories = [];

    public IReadOnlyList<Category> Categories => _categories;

    public IReadOnlyList<Subcategory> Subcategories => _subcategories;

    public Task<IReadOnlyList<CategoryView>> ListCategoriesAsync(Guid tenantId, bool includeInactive, CancellationToken cancellationToken)
    {
        IReadOnlyList<CategoryView> result = _categories
            .Where(c => !_deletedCategories.Contains(c.Id) && (includeInactive || c.IsActive))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new CategoryView(c, CountLive(c.Id)))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<Category?> FindCategoryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(LiveCategory(id));

    public Task<bool> CategoryNameTakenAsync(Guid tenantId, string name, Guid exceptId, CancellationToken cancellationToken)
        => Task.FromResult(_categories.Any(c =>
            c.Id != exceptId
            && c.IsActive
            && !_deletedCategories.Contains(c.Id)
            && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)));

    public Task<int> CountSubcategoriesAsync(Guid tenantId, Guid categoryId, CancellationToken cancellationToken)
        => Task.FromResult(CountLive(categoryId));

    public Task InsertCategoryAsync(Category category, CancellationToken cancellationToken)
    {
        _categories.Add(category);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateCategoryAsync(Category category, long expectedRowVersion, CancellationToken cancellationToken)
    {
        var index = _categories.FindIndex(c => c.Id == category.Id && c.RowVersion == expectedRowVersion);
        if (index < 0 || _deletedCategories.Contains(category.Id))
        {
            return Task.FromResult(false);
        }

        _categories[index] = category;
        return Task.FromResult(true);
    }

    public Task<bool> SoftDeleteCategoryAsync(
        Guid tenantId,
        Guid id,
        long expectedRowVersion,
        Guid? deletedBy,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken)
    {
        var live = LiveCategory(id);
        if (live is null || live.RowVersion != expectedRowVersion)
        {
            return Task.FromResult(false);
        }

        _deletedCategories.Add(id);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<SubcategoryView>> ListSubcategoriesAsync(
        Guid tenantId,
        Guid? categoryId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SubcategoryView> result = _subcategories
            .Where(s => !_deletedSubcategories.Contains(s.Id))
            .Where(s => categoryId is null || s.CategoryId == categoryId)
            .Where(s => includeInactive || s.IsActive)
            .Select(s => new SubcategoryView(s, _categories.Single(c => c.Id == s.CategoryId).Name))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<Subcategory?> FindSubcategoryAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(LiveSubcategory(id));

    public Task<bool> SubcategoryNameTakenAsync(
        Guid tenantId,
        Guid categoryId,
        string name,
        Guid exceptId,
        CancellationToken cancellationToken)
        => Task.FromResult(_subcategories.Any(s =>
            s.Id != exceptId
            && s.CategoryId == categoryId
            && s.IsActive
            && !_deletedSubcategories.Contains(s.Id)
            && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)));

    public Task InsertSubcategoryAsync(Subcategory subcategory, CancellationToken cancellationToken)
    {
        _subcategories.Add(subcategory);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateSubcategoryAsync(Subcategory subcategory, long expectedRowVersion, CancellationToken cancellationToken)
    {
        var index = _subcategories.FindIndex(s => s.Id == subcategory.Id && s.RowVersion == expectedRowVersion);
        if (index < 0 || _deletedSubcategories.Contains(subcategory.Id))
        {
            return Task.FromResult(false);
        }

        _subcategories[index] = subcategory;
        return Task.FromResult(true);
    }

    public Task<bool> SoftDeleteSubcategoryAsync(
        Guid tenantId,
        Guid id,
        long expectedRowVersion,
        Guid? deletedBy,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken)
    {
        var live = LiveSubcategory(id);
        if (live is null || live.RowVersion != expectedRowVersion)
        {
            return Task.FromResult(false);
        }

        _deletedSubcategories.Add(id);
        return Task.FromResult(true);
    }

    private Category? LiveCategory(Guid id)
        => _categories.FirstOrDefault(c => c.Id == id && !_deletedCategories.Contains(id));

    private Subcategory? LiveSubcategory(Guid id)
        => _subcategories.FirstOrDefault(s => s.Id == id && !_deletedSubcategories.Contains(id));

    private int CountLive(Guid categoryId)
        => _subcategories.Count(s => s.CategoryId == categoryId && !_deletedSubcategories.Contains(s.Id));
}

/// <summary>Usage checker that reports the usages it is given, and records what it was asked about.</summary>
internal sealed class FakeUsageChecker : IConfigurationUsageChecker
{
    public List<ConfigurationEntity> Asked { get; } = [];

    public IReadOnlyList<ConfigurationUsage> Usages { get; set; } = [];

    public Task<IReadOnlyList<ConfigurationUsage>> FindUsagesAsync(
        ConfigurationEntity entity,
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        Asked.Add(entity);
        return Task.FromResult(Usages);
    }
}
