using Edm.Audit.Application;
using Edm.Configuration.Domain.Catalogue;
using Edm.SharedKernel.Context;
using Edm.SharedKernel.Errors;

namespace Edm.Configuration.Application.Catalogue;

/// <summary>
/// Categories (Master Prompt Part 5.1). Deactivate, do not delete, when a category is still in use.
/// Every change writes one audit event with the old and new values.
/// </summary>
public sealed class CategoryService(
    ICatalogueStore store,
    IConfigurationUsageChecker usages,
    IAuditWriter audit,
    ITenantContext tenant,
    IActorContext actor,
    IClock clock)
{
    private const string DuplicateMessage = "A category with this name already exists.";

    public Task<IReadOnlyList<CategoryView>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
        => store.ListCategoriesAsync(tenant.TenantId, includeInactive, cancellationToken);

    public async Task<CategoryView> CreateAsync(CategoryCreate input, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var nameError = CatalogueRules.NameError(input.Name);
        AddError(errors, "name", nameError);
        AddError(errors, "description", CatalogueRules.DescriptionError(input.Description));

        if (nameError is null)
        {
            var name = CatalogueRules.NormaliseName(input.Name!);
            if (await store.CategoryNameTakenAsync(tenant.TenantId, name, Guid.Empty, cancellationToken))
            {
                AddError(errors, "name", DuplicateMessage);
            }
        }

        ThrowIfInvalid(errors);

        var category = new Category(
            Guid.CreateVersion7(),
            tenant.TenantId,
            CatalogueRules.NormaliseName(input.Name!),
            CatalogueRules.NormaliseDescription(input.Description),
            IsActive: true,
            RowVersion: 1,
            UpdatedAtUtc: clock.UtcNow,
            UpdatedBy: actor.UserId);

        await store.InsertCategoryAsync(category, cancellationToken);
        await audit.WriteAsync(
            CatalogueAudit.Draft("Category", category.Id, "Created", before: null, after: Snapshot(category)),
            cancellationToken);

        return new CategoryView(category, SubcategoryCount: 0);
    }

    /// <summary>Returns null when the category does not exist (or is deleted).</summary>
    public async Task<CategoryView?> UpdateAsync(Guid id, CategoryUpdate input, CancellationToken cancellationToken)
    {
        var existing = await store.FindCategoryAsync(tenant.TenantId, id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var nameError = CatalogueRules.NameError(input.Name);
        AddError(errors, "name", nameError);
        AddError(errors, "description", CatalogueRules.DescriptionError(input.Description));

        var isActive = input.IsActive ?? existing.IsActive;
        if (nameError is null && isActive)
        {
            var name = CatalogueRules.NormaliseName(input.Name!);
            if (await store.CategoryNameTakenAsync(tenant.TenantId, name, id, cancellationToken))
            {
                AddError(errors, "name", DuplicateMessage);
            }
        }

        ThrowIfInvalid(errors);

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

        if (!await store.UpdateCategoryAsync(updated, input.RowVersion, cancellationToken))
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        await audit.WriteAsync(
            CatalogueAudit.Draft(
                "Category",
                id,
                CatalogueAudit.ActionFor(existing.IsActive, updated.IsActive),
                Snapshot(existing),
                Snapshot(updated)),
            cancellationToken);

        var count = await store.CountSubcategoriesAsync(tenant.TenantId, id, cancellationToken);
        return new CategoryView(updated, count);
    }

    /// <summary>Soft delete. Returns false when the category does not exist.</summary>
    public async Task<bool> DeleteAsync(Guid id, long rowVersion, CancellationToken cancellationToken)
    {
        var existing = await store.FindCategoryAsync(tenant.TenantId, id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        if (existing.RowVersion != rowVersion)
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        if (await store.CountSubcategoriesAsync(tenant.TenantId, id, cancellationToken) > 0)
        {
            throw new EdmException(EdmErrorCodes.RecordInUse);
        }

        var usage = await usages.FindUsagesAsync(ConfigurationEntity.Category, tenant.TenantId, id, cancellationToken);
        if (usage.Count > 0)
        {
            throw new EdmException(EdmErrorCodes.RecordInUse);
        }

        if (!await store.SoftDeleteCategoryAsync(
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
            CatalogueAudit.Draft("Category", id, "Deleted", before: Snapshot(existing), after: null),
            cancellationToken);
        return true;
    }

    private static CategorySnapshot Snapshot(Category category)
        => new(category.Name, category.Description, category.IsActive);

    internal static void AddError(Dictionary<string, string[]> errors, string field, string? message)
    {
        if (message is not null)
        {
            errors[field] = [message];
        }
    }

    internal static void ThrowIfInvalid(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw new EdmException(EdmErrorCodes.ValidationFailed, errors);
        }
    }
}
