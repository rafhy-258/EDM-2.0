using Edm.Audit.Application;
using Edm.Identity.Domain.Organisation;
using Edm.SharedKernel.Context;
using Edm.SharedKernel.Errors;

namespace Edm.Identity.Application.Organisation;

/// <summary>
/// The organisation user directory (data model section 6). Each person belongs to one active department. A person is
/// deactivated, not deleted. Every change writes one audit event with the old and new values.
/// </summary>
public sealed class DirectoryUserService(
    IOrganisationStore store,
    IAuditWriter audit,
    ITenantContext tenant,
    IActorContext actor,
    IClock clock)
{
    private const string DepartmentMessage = "Choose an active department.";
    private const string EmployeeTakenMessage =
        "This employee ID already exists. Reactivate the existing record instead.";

    public Task<IReadOnlyList<DirectoryUser>> ListAsync(Guid? departmentId, bool includeInactive, CancellationToken cancellationToken)
        => store.ListUsersAsync(tenant.TenantId, departmentId, includeInactive, cancellationToken);

    public async Task<DirectoryUser> CreateAsync(DirectoryUserCreate input, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        AddError(errors, "employeeId", OrganisationRules.EmployeeIdError(input.EmployeeId));
        AddError(errors, "name", OrganisationRules.NameError(input.Name));
        AddError(errors, "email", OrganisationRules.EmailError(input.Email));
        await CheckDepartmentAsync(errors, input.DepartmentId, cancellationToken);

        if (!errors.ContainsKey("employeeId"))
        {
            var employeeId = OrganisationRules.NormaliseEmployeeId(input.EmployeeId!);
            if (await store.EmployeeIdTakenAsync(tenant.TenantId, employeeId, Guid.Empty, cancellationToken))
            {
                AddError(errors, "employeeId", EmployeeTakenMessage);
            }
        }

        ThrowIfInvalid(errors);

        var user = new DirectoryUser(
            Guid.CreateVersion7(),
            tenant.TenantId,
            OrganisationRules.NormaliseEmployeeId(input.EmployeeId!),
            OrganisationRules.NormaliseText(input.Name!),
            OrganisationRules.NormaliseEmail(input.Email),
            input.DepartmentId!.Value,
            IsActive: true,
            RowVersion: 1,
            UpdatedAtUtc: clock.UtcNow,
            UpdatedBy: actor.UserId);

        await store.InsertUserAsync(user, cancellationToken);
        await audit.WriteAsync(
            OrganisationAudit.Draft("DirectoryUser", user.Id, "Created", before: null, after: Snapshot(user)),
            cancellationToken);

        return user;
    }

    /// <summary>Returns null when the person does not exist.</summary>
    public async Task<DirectoryUser?> UpdateAsync(Guid id, DirectoryUserUpdate input, CancellationToken cancellationToken)
    {
        var existing = await store.FindUserAsync(tenant.TenantId, id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var employeeId = input.EmployeeId ?? existing.EmployeeId;
        var name = input.Name ?? existing.Name;
        var email = input.Email ?? existing.Email;
        var departmentId = input.DepartmentId ?? existing.DepartmentId;
        var isActive = input.IsActive ?? existing.IsActive;

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        AddError(errors, "employeeId", OrganisationRules.EmployeeIdError(employeeId));
        AddError(errors, "name", OrganisationRules.NameError(name));
        AddError(errors, "email", OrganisationRules.EmailError(email));

        // A department is checked again when it changes or when the person is reactivated.
        if (departmentId != existing.DepartmentId || (isActive && !existing.IsActive))
        {
            await CheckDepartmentAsync(errors, departmentId, cancellationToken);
        }

        if (!errors.ContainsKey("employeeId"))
        {
            var normalised = OrganisationRules.NormaliseEmployeeId(employeeId);
            if (await store.EmployeeIdTakenAsync(tenant.TenantId, normalised, id, cancellationToken))
            {
                AddError(errors, "employeeId", EmployeeTakenMessage);
            }
        }

        ThrowIfInvalid(errors);

        if (existing.RowVersion != input.RowVersion)
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        var updated = existing with
        {
            EmployeeId = OrganisationRules.NormaliseEmployeeId(employeeId),
            Name = OrganisationRules.NormaliseText(name),
            Email = OrganisationRules.NormaliseEmail(email),
            DepartmentId = departmentId,
            IsActive = isActive,
            RowVersion = existing.RowVersion + 1,
            UpdatedAtUtc = clock.UtcNow,
            UpdatedBy = actor.UserId,
        };

        if (!await store.UpdateUserAsync(updated, input.RowVersion, cancellationToken))
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        await audit.WriteAsync(
            OrganisationAudit.Draft(
                "DirectoryUser",
                id,
                OrganisationAudit.ActionFor(existing.IsActive, updated.IsActive),
                Snapshot(existing),
                Snapshot(updated)),
            cancellationToken);

        return updated;
    }

    private async Task CheckDepartmentAsync(
        Dictionary<string, string[]> errors,
        Guid? departmentId,
        CancellationToken cancellationToken)
    {
        if (departmentId is null)
        {
            AddError(errors, "departmentId", "Choose a department.");
            return;
        }

        var department = await store.FindDepartmentAsync(tenant.TenantId, departmentId.Value, cancellationToken);
        if (department is null || !department.IsActive)
        {
            AddError(errors, "departmentId", DepartmentMessage);
        }
    }

    private static DirectoryUserSnapshot Snapshot(DirectoryUser user)
        => new(user.EmployeeId, user.Name, user.Email, user.DepartmentId, user.IsActive);

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
