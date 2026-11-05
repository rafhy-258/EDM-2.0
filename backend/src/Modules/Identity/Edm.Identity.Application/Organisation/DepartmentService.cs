using Edm.Audit.Application;
using Edm.Identity.Domain.Organisation;
using Edm.SharedKernel.Context;
using Edm.SharedKernel.Errors;

namespace Edm.Identity.Application.Organisation;

/// <summary>
/// Departments (Master Prompt Part 14.1). A department is deactivated, not deleted. It cannot be deactivated while it has
/// active people. Every change writes one audit event with the old and new values.
/// </summary>
public sealed class DepartmentService(
    IOrganisationStore store,
    IAuditWriter audit,
    ITenantContext tenant,
    IActorContext actor,
    IClock clock)
{
    private const string DuplicateMessage = "A department with this name already exists.";
    private const string InUseMessage = "Move or deactivate the people in this department first.";

    public Task<IReadOnlyList<Department>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
        => store.ListDepartmentsAsync(tenant.TenantId, includeInactive, cancellationToken);

    public async Task<Department> CreateAsync(DepartmentCreate input, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var nameError = OrganisationRules.DepartmentNameError(input.Name);
        AddError(errors, "name", nameError);

        if (nameError is null)
        {
            var name = OrganisationRules.NormaliseText(input.Name!);
            if (await store.DepartmentNameTakenAsync(tenant.TenantId, name, Guid.Empty, cancellationToken))
            {
                AddError(errors, "name", DuplicateMessage);
            }
        }

        ThrowIfInvalid(errors);

        var department = new Department(
            Guid.CreateVersion7(),
            tenant.TenantId,
            OrganisationRules.NormaliseText(input.Name!),
            IsActive: true,
            RowVersion: 1,
            UpdatedAtUtc: clock.UtcNow,
            UpdatedBy: actor.UserId);

        await store.InsertDepartmentAsync(department, cancellationToken);
        await audit.WriteAsync(
            OrganisationAudit.Draft("Department", department.Id, "Created", before: null, after: Snapshot(department)),
            cancellationToken);

        return department;
    }

    /// <summary>Returns null when the department does not exist.</summary>
    public async Task<Department?> UpdateAsync(Guid id, DepartmentUpdate input, CancellationToken cancellationToken)
    {
        var existing = await store.FindDepartmentAsync(tenant.TenantId, id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var nameError = OrganisationRules.DepartmentNameError(input.Name);
        AddError(errors, "name", nameError);

        var isActive = input.IsActive ?? existing.IsActive;
        if (nameError is null && isActive)
        {
            var name = OrganisationRules.NormaliseText(input.Name!);
            if (await store.DepartmentNameTakenAsync(tenant.TenantId, name, id, cancellationToken))
            {
                AddError(errors, "name", DuplicateMessage);
            }
        }

        if (existing.IsActive && !isActive
            && await store.CountActiveUsersInDepartmentAsync(tenant.TenantId, id, cancellationToken) > 0)
        {
            AddError(errors, "isActive", InUseMessage);
        }

        ThrowIfInvalid(errors);

        if (existing.RowVersion != input.RowVersion)
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        var updated = existing with
        {
            Name = OrganisationRules.NormaliseText(input.Name!),
            IsActive = isActive,
            RowVersion = existing.RowVersion + 1,
            UpdatedAtUtc = clock.UtcNow,
            UpdatedBy = actor.UserId,
        };

        if (!await store.UpdateDepartmentAsync(updated, input.RowVersion, cancellationToken))
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        await audit.WriteAsync(
            OrganisationAudit.Draft(
                "Department",
                id,
                OrganisationAudit.ActionFor(existing.IsActive, updated.IsActive),
                Snapshot(existing),
                Snapshot(updated)),
            cancellationToken);

        return updated;
    }

    private static DepartmentSnapshot Snapshot(Department department) => new(department.Name, department.IsActive);

    private static void AddError(Dictionary<string, string[]> errors, string field, string? message)
    {
        if (message is not null)
        {
            errors[field] = [message];
        }
    }

    private static void ThrowIfInvalid(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw new EdmException(EdmErrorCodes.ValidationFailed, errors);
        }
    }
}
