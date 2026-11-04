using Edm.Audit.Application;
using Edm.Configuration.Domain.Catalogue;
using Edm.SharedKernel.Context;
using Edm.SharedKernel.Errors;

namespace Edm.Configuration.Application.Catalogue;

/// <summary>
/// Subcategories (Master Prompt Part 5.1). A subcategory belongs to one active category. Names are unique within the
/// category. Deactivate, do not delete, when a subcategory is still in use.
/// </summary>
public sealed class SubcategoryService(
    ICatalogueStore store,
    IConfigurationUsageChecker usages,
    IAuditWriter audit,
    ITenantContext tenant,
    IActorContext actor,
    IClock clock)
{
    private const string DuplicateMessage = "A subcategory with this name already exists in this category.";
    private const string CategoryMessage = "Choose an active category.";

    public Task<IReadOnlyList<SubcategoryView>> ListAsync(Guid? categoryId, bool includeInactive, CancellationToken cancellationToken)
        => store.ListSubcategoriesAsync(tenant.TenantId, categoryId, includeInactive, cancellationToken);

    public async Task<SubcategoryView> CreateAsync(SubcategoryCreate input, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var nameError = CatalogueRules.NameError(input.Name);
        CategoryService.AddError(errors, "name", nameError);
        CategoryService.AddError(errors, "description", CatalogueRules.DescriptionError(input.Description));

        Category? category = null;
        if (input.CategoryId is Guid categoryId && categoryId != Guid.Empty)
        {
            category = await store.FindCategoryAsync(tenant.TenantId, categoryId, cancellationToken);
        }

        if (category is null || !category.IsActive)
        {
            CategoryService.AddError(errors, "categoryId", CategoryMessage);
        }

        if (nameError is null && category is not null && category.IsActive)
        {
            var name = CatalogueRules.NormaliseName(input.Name!);
            if (await store.SubcategoryNameTakenAsync(tenant.TenantId, category.Id, name, Guid.Empty, cancellationToken))
            {
                CategoryService.AddError(errors, "name", DuplicateMessage);
            }
        }

        CategoryService.ThrowIfInvalid(errors);

        // ThrowIfInvalid has already refused a missing or inactive category, so this cannot be null.
        var parent = category ?? throw new InvalidOperationException("The category was validated above.");

        var subcategory = new Subcategory(
            Guid.CreateVersion7(),
            tenant.TenantId,
            parent.Id,
            CatalogueRules.NormaliseName(input.Name!),
            CatalogueRules.NormaliseDescription(input.Description),
            IsActive: true,
            RowVersion: 1,
            UpdatedAtUtc: clock.UtcNow,
            UpdatedBy: actor.UserId);

        await store.InsertSubcategoryAsync(subcategory, cancellationToken);
        await audit.WriteAsync(
            CatalogueAudit.Draft("Subcategory", subcategory.Id, "Created", before: null, after: Snapshot(subcategory)),
            cancellationToken);

        return new SubcategoryView(subcategory, parent.Name);
    }

    /// <summary>Returns null when the subcategory does not exist (or is deleted).</summary>
    public async Task<SubcategoryView?> UpdateAsync(Guid id, SubcategoryUpdate input, CancellationToken cancellationToken)
    {
        var existing = await store.FindSubcategoryAsync(tenant.TenantId, id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var nameError = CatalogueRules.NameError(input.Name);
        CategoryService.AddError(errors, "name", nameError);
        CategoryService.AddError(errors, "description", CatalogueRules.DescriptionError(input.Description));

        var isActive = input.IsActive ?? existing.IsActive;
        if (nameError is null && isActive)
        {
            var name = CatalogueRules.NormaliseName(input.Name!);
            if (await store.SubcategoryNameTakenAsync(tenant.TenantId, existing.CategoryId, name, id, cancellationToken))
            {
                CategoryService.AddError(errors, "name", DuplicateMessage);
            }
        }

        CategoryService.ThrowIfInvalid(errors);

        if (existing.RowVersion != input.RowVersion)
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        var updated = existing with
        {
            Name = CatalogueRules.NormaliseName(input.Name!),
            Description = CatalogueRules.NormaliseDescription(input.Description),
            IsActive = isActive,
            RowVersion = existing.RowVersion + 1,
            UpdatedAtUtc = clock.UtcNow,
            UpdatedBy = actor.UserId,
        };

        if (!await store.UpdateSubcategoryAsync(updated, input.RowVersion, cancellationToken))
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        await audit.WriteAsync(
            CatalogueAudit.Draft(
                "Subcategory",
                id,
                CatalogueAudit.ActionFor(existing.IsActive, updated.IsActive),
                Snapshot(existing),
                Snapshot(updated)),
            cancellationToken);

        var category = await store.FindCategoryAsync(tenant.TenantId, updated.CategoryId, cancellationToken);
        return new SubcategoryView(updated, category?.Name ?? string.Empty);
    }

    /// <summary>Soft delete. Returns false when the subcategory does not exist.</summary>
    public async Task<bool> DeleteAsync(Guid id, long rowVersion, CancellationToken cancellationToken)
    {
        var existing = await store.FindSubcategoryAsync(tenant.TenantId, id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        if (existing.RowVersion != rowVersion)
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        var usage = await usages.FindUsagesAsync(ConfigurationEntity.Subcategory, tenant.TenantId, id, cancellationToken);
        if (usage.Count > 0)
        {
            throw new EdmException(EdmErrorCodes.RecordInUse);
        }

        if (!await store.SoftDeleteSubcategoryAsync(
                tenant.TenantId,
                id,
                rowVersion,
                actor.UserId,
                clock.UtcNow,
                cancellationToken))
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        await audit.WriteAsync(
            CatalogueAudit.Draft("Subcategory", id, "Deleted", before: Snapshot(existing), after: null),
            cancellationToken);
        return true;
    }

    private static SubcategorySnapshot Snapshot(Subcategory subcategory)
        => new(subcategory.CategoryId, subcategory.Name, subcategory.Description, subcategory.IsActive);
}
