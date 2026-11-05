using Edm.Audit.Application;
using Edm.Configuration.Application.Catalogue;
using Edm.Configuration.Domain.Authorities;
using Edm.SharedKernel.Context;
using Edm.SharedKernel.Errors;

namespace Edm.Configuration.Application.Authorities;

public sealed record EcMemberView(EcMember Member, AuthorityDirectoryUser User);

public sealed record RecommendingAuthorityView(
    RecommendingAuthority Authority,
    AuthorityDirectoryUser User,
    string ActionTagSetName);

public sealed class ApprovalAuthorityService(
    IApprovalAuthorityStore store,
    IApprovalAuthorityDirectory directory,
    IConfigurationUsageChecker usages,
    IAuditWriter audit,
    ITenantContext tenant,
    IActorContext actor,
    IClock clock)
{
    private const string DuplicateMemberMessage =
        "This member already exists with this designation. Reactivate the existing record instead.";
    private const string DuplicateAuthorityMessage =
        "This department and user already have a recommending authority.";

    public async Task<IReadOnlyList<EcMemberView>> ListMembersAsync(
        string? designation,
        bool includeInactive,
        string? search,
        CancellationToken cancellationToken)
    {
        if (designation is not null && ApprovalAuthorityRules.DesignationError(designation) is not null)
        {
            ThrowFieldError("designation", "Choose a valid designation.");
        }

        var members = await store.ListEcMembersAsync(
            tenant.TenantId, designation, includeInactive, NormaliseSearch(search), cancellationToken);
        var users = await directory.ListUsersAsync(tenant.TenantId, null, includeInactive: true, cancellationToken);
        var usersById = users.ToDictionary(user => user.Id);
        return members.Select(member =>
        {
            if (!usersById.TryGetValue(member.UserId, out var user))
            {
                throw new InvalidOperationException(
                    $"EC member {member.Id} references missing directory user {member.UserId}.");
            }

            return new EcMemberView(member, user);
        }).ToArray();
    }

    public async Task<IReadOnlyList<AuthorityDirectoryUser>> ListCandidatesAsync(
        Guid? departmentId,
        CancellationToken cancellationToken)
    {
        var users = await directory.ListUsersAsync(tenant.TenantId, departmentId, includeInactive: false, cancellationToken);
        return users.Where(user => user.IsActive && user.IsDepartmentActive).ToArray();
    }

    public async Task<IReadOnlyList<AuthorityDirectoryDepartment>> ListDepartmentsAsync(CancellationToken cancellationToken)
    {
        var departments = await directory.ListDepartmentsAsync(tenant.TenantId, includeInactive: false, cancellationToken);
        return departments.Where(department => department.IsActive).ToArray();
    }

    public async Task<AuthorityDirectoryUser> FindDirectoryUserAsync(Guid userId, CancellationToken cancellationToken)
        => await directory.FindUserAsync(tenant.TenantId, userId, cancellationToken)
            ?? throw new InvalidOperationException($"Directory user {userId} was not found for tenant {tenant.TenantId}.");

    public async Task<string> FindActionTagSetNameAsync(Guid id, CancellationToken cancellationToken)
    {
        var tagSets = await store.ListActionTagSetsAsync(tenant.TenantId, cancellationToken);
        return tagSets.FirstOrDefault(set => set.Id == id)?.Name
            ?? throw new InvalidOperationException($"Action-tag set {id} was not found for tenant {tenant.TenantId}.");
    }

    public async Task<EcMember> CreateMemberAsync(EcMemberCreate input, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        AddError(errors, "userId", input.UserId is null || input.UserId == Guid.Empty ? "Choose an active directory user." : null);
        AddError(errors, "designation", ApprovalAuthorityRules.DesignationError(input.Designation));
        ThrowIfInvalid(errors);

        var user = await FindEligibleUserAsync(input.UserId!.Value, "userId", cancellationToken);
        var existing = await store.FindEcMemberAsync(
            tenant.TenantId, input.UserId.Value, input.Designation!, cancellationToken);
        if (existing is not null)
        {
            ThrowFieldError("userId", DuplicateMemberMessage);
        }

        if (input.Designation != ApprovalDesignationCodes.EcMember &&
            await store.HasActiveHolderAsync(tenant.TenantId, input.Designation!, cancellationToken))
        {
            throw new EdmException(EdmErrorCodes.SingleHolderAlreadyActive);
        }

        var member = new EcMember(
            Guid.CreateVersion7(),
            tenant.TenantId,
            user.Id,
            input.Designation!,
            true,
            1,
            clock.UtcNow,
            actor.UserId);
        await store.InsertEcMemberAsync(member, cancellationToken);
        await WriteAuditAsync("EcMember", member.Id, "Created", before: null, MemberSnapshot(member, user), cancellationToken);
        return member;
    }

    public async Task<EcMember?> UpdateMemberAsync(
        Guid id,
        EcMemberChange input,
        CancellationToken cancellationToken)
    {
        var existing = await store.FindEcMemberAsync(tenant.TenantId, id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        if (existing.RowVersion != input.RowVersion)
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        var isActive = input.IsActive ?? existing.IsActive;
        if (isActive == existing.IsActive)
        {
            return existing;
        }

        var user = isActive
            ? await FindEligibleUserAsync(existing.UserId, "userId", cancellationToken)
            : await directory.FindUserAsync(tenant.TenantId, existing.UserId, cancellationToken);
        if (isActive && existing.Designation != ApprovalDesignationCodes.EcMember &&
            await store.HasActiveHolderAsync(tenant.TenantId, existing.Designation, cancellationToken))
        {
            throw new EdmException(EdmErrorCodes.SingleHolderAlreadyActive);
        }

        var updated = existing with
        {
            IsActive = isActive,
            RowVersion = existing.RowVersion + 1,
            UpdatedAtUtc = clock.UtcNow,
            UpdatedBy = actor.UserId,
        };
        if (!await store.UpdateEcMemberAsync(updated, input.RowVersion, cancellationToken))
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        await WriteAuditAsync(
            "EcMember", id, CatalogueAudit.ActionFor(existing.IsActive, isActive),
            MemberSnapshot(existing, null), MemberSnapshot(updated, user), cancellationToken);
        return updated;
    }

    public async Task<bool> DeleteMemberAsync(Guid id, long rowVersion, CancellationToken cancellationToken)
    {
        var existing = await store.FindEcMemberAsync(tenant.TenantId, id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        EnsureVersion(existing.RowVersion, rowVersion);
        if ((await usages.FindUsagesAsync(ConfigurationEntity.EcMember, tenant.TenantId, id, cancellationToken)).Count > 0)
        {
            throw new EdmException(EdmErrorCodes.RecordInUse);
        }

        if (!await store.SoftDeleteEcMemberAsync(tenant.TenantId, id, rowVersion, actor.UserId, clock.UtcNow, cancellationToken))
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        await WriteAuditAsync("EcMember", id, "Deleted", MemberSnapshot(existing, null), after: null, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<RecommendingAuthorityView>> ListAuthoritiesAsync(
        bool includeInactive,
        string? search,
        CancellationToken cancellationToken)
    {
        await EnsureActionTagSetsAsync(cancellationToken);
        var authorities = await store.ListRecommendingAuthoritiesAsync(
            tenant.TenantId, includeInactive, NormaliseSearch(search), cancellationToken);
        var users = await directory.ListUsersAsync(tenant.TenantId, null, includeInactive: true, cancellationToken);
        var usersById = users.ToDictionary(user => user.Id);
        var tagSets = await store.ListActionTagSetsAsync(tenant.TenantId, cancellationToken);
        var tagNames = tagSets.ToDictionary(set => set.Id, set => set.Name);
        return authorities.Select(authority =>
        {
            if (!tagNames.TryGetValue(authority.DefaultActionTagSetId, out var name))
            {
                throw new InvalidOperationException(
                    $"The recommending authority {authority.Id} references a missing action-tag set.");
            }

            if (!usersById.TryGetValue(authority.UserId, out var user))
            {
                throw new InvalidOperationException(
                    $"Recommending authority {authority.Id} references missing directory user {authority.UserId}.");
            }

            return new RecommendingAuthorityView(authority, user, name);
        }).ToArray();
    }

    public async Task<RecommendingAuthority> CreateAuthorityAsync(
        RecommendingAuthorityCreate input,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        AddError(errors, "title", ApprovalAuthorityRules.TitleError(input.Title));
        AddError(errors, "departmentId", input.DepartmentId is null || input.DepartmentId == Guid.Empty
            ? "Choose an active department." : null);
        AddError(errors, "userId", input.UserId is null || input.UserId == Guid.Empty
            ? "Choose an active user in this department." : null);
        ThrowIfInvalid(errors);

        var title = ApprovalAuthorityRules.NormaliseTitle(input.Title!);
        var user = await FindEligibleUserAsync(input.UserId!.Value, "userId", cancellationToken);
        ValidateDepartmentUser(input.DepartmentId!.Value, user);
        if (await store.RecommendingAuthorityTakenAsync(
                tenant.TenantId, input.DepartmentId.Value, user.Id, Guid.Empty, cancellationToken))
        {
            ThrowFieldError("userId", DuplicateAuthorityMessage);
        }

        await EnsureActionTagSetsAsync(cancellationToken);
        var actionTagSetId = await store.FindActionTagSetIdAsync(
            tenant.TenantId, ApprovalDesignationCodes.RecommendingAuthority, cancellationToken)
            ?? throw new InvalidOperationException("The default recommending-authority action-tag set was not initialized.");
        var now = clock.UtcNow;
        var authority = new RecommendingAuthority(
            Guid.CreateVersion7(),
            tenant.TenantId,
            title,
            input.DepartmentId.Value,
            user.Id,
            actionTagSetId,
            true,
            1,
            now,
            actor.UserId);
        await store.InsertRecommendingAuthorityAsync(authority, cancellationToken);
        await WriteAuditAsync("RecommendingAuthority", authority.Id, "Created", before: null, AuthoritySnapshot(authority, user), cancellationToken);
        return authority;
    }

    public async Task<RecommendingAuthority?> UpdateAuthorityAsync(
        Guid id,
        RecommendingAuthorityChange input,
        CancellationToken cancellationToken)
    {
        var existing = await store.FindRecommendingAuthorityAsync(tenant.TenantId, id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        EnsureVersion(existing.RowVersion, input.RowVersion);
        var departmentId = input.DepartmentId ?? existing.DepartmentId;
        var userId = input.UserId ?? existing.UserId;
        var isActive = input.IsActive ?? existing.IsActive;
        var changed = departmentId != existing.DepartmentId || userId != existing.UserId || isActive != existing.IsActive;
        if (!changed)
        {
            return existing;
        }

        var assignmentChanged = departmentId != existing.DepartmentId || userId != existing.UserId;
        var user = isActive || assignmentChanged
            ? await FindEligibleUserAsync(userId, "userId", cancellationToken)
            : await directory.FindUserAsync(tenant.TenantId, userId, cancellationToken);
        if (isActive || assignmentChanged)
        {
            ValidateDepartmentUser(departmentId, user!);
        }
        if (isActive && await store.RecommendingAuthorityTakenAsync(
                tenant.TenantId, departmentId, userId, id, cancellationToken))
        {
            ThrowFieldError("userId", DuplicateAuthorityMessage);
        }

        var updated = existing with
        {
            DepartmentId = departmentId,
            UserId = userId,
            IsActive = isActive,
            RowVersion = existing.RowVersion + 1,
            UpdatedAtUtc = clock.UtcNow,
            UpdatedBy = actor.UserId,
        };
        if (!await store.UpdateRecommendingAuthorityAsync(updated, input.RowVersion, cancellationToken))
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        await WriteAuditAsync(
            "RecommendingAuthority", id, CatalogueAudit.ActionFor(existing.IsActive, isActive),
            AuthoritySnapshot(existing, await directory.FindUserAsync(tenant.TenantId, existing.UserId, cancellationToken)),
            AuthoritySnapshot(updated, user),
            cancellationToken);
        return updated;
    }

    public async Task<bool> DeleteAuthorityAsync(Guid id, long rowVersion, CancellationToken cancellationToken)
    {
        var existing = await store.FindRecommendingAuthorityAsync(tenant.TenantId, id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        EnsureVersion(existing.RowVersion, rowVersion);
        if ((await usages.FindUsagesAsync(ConfigurationEntity.RecommendingAuthority, tenant.TenantId, id, cancellationToken)).Count > 0)
        {
            throw new EdmException(EdmErrorCodes.RecordInUse);
        }

        if (!await store.SoftDeleteRecommendingAuthorityAsync(
                tenant.TenantId, id, rowVersion, actor.UserId, clock.UtcNow, cancellationToken))
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        await WriteAuditAsync("RecommendingAuthority", id, "Deleted", AuthoritySnapshot(existing, null), after: null, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<ActionTagSet>> ListActionTagSetsAsync(CancellationToken cancellationToken)
    {
        await EnsureActionTagSetsAsync(cancellationToken);
        return await store.ListActionTagSetsAsync(tenant.TenantId, cancellationToken);
    }

    public async Task<ActionTagSet?> UpdateActionTagSetAsync(
        Guid id,
        ActionTagSetChange input,
        CancellationToken cancellationToken)
    {
        await EnsureActionTagSetsAsync(cancellationToken);
        var existing = await store.FindActionTagSetAsync(tenant.TenantId, id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var error = ApprovalAuthorityRules.ActionSetError(existing.Designation, input.AllowedActions);
        if (error is not null)
        {
            ThrowFieldError("allowedActions", error);
        }

        EnsureVersion(existing.RowVersion, input.RowVersion);
        var actions = input.AllowedActions!.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (existing.AllowedActions.Order(StringComparer.Ordinal).SequenceEqual(actions, StringComparer.Ordinal))
        {
            return existing;
        }

        var updated = existing with
        {
            AllowedActions = actions,
            RowVersion = existing.RowVersion + 1,
            UpdatedAtUtc = clock.UtcNow,
            UpdatedBy = actor.UserId,
        };
        if (!await store.UpdateActionTagSetAsync(updated, input.RowVersion, cancellationToken))
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        await WriteAuditAsync("ActionTagSet", id, "Updated", TagSnapshot(existing), TagSnapshot(updated), cancellationToken);
        return updated;
    }

    private Task EnsureActionTagSetsAsync(CancellationToken cancellationToken)
        => store.EnsureDefaultActionTagSetsAsync(tenant.TenantId, clock.UtcNow, cancellationToken);

    private async Task<AuthorityDirectoryUser> FindEligibleUserAsync(
        Guid userId,
        string field,
        CancellationToken cancellationToken)
    {
        var user = await directory.FindUserAsync(tenant.TenantId, userId, cancellationToken);
        if (user is null || !user.IsActive || !user.IsDepartmentActive)
        {
            ThrowFieldError(field, "Choose an active user in an active department.");
        }

        return user!;
    }

    private static void ValidateDepartmentUser(Guid departmentId, AuthorityDirectoryUser user)
    {
        if (user.DepartmentId != departmentId)
        {
            ThrowFieldError("userId", "Choose a user who belongs to the selected department.");
        }

        if (!user.IsDepartmentActive)
        {
            ThrowFieldError("departmentId", "Choose an active department.");
        }
    }

    private async Task WriteAuditAsync(
        string entityType,
        Guid id,
        string action,
        object? before,
        object? after,
        CancellationToken cancellationToken)
    {
        await audit.WriteAsync(new AuditEventDraft
        {
            Module = CatalogueAudit.Module,
            EntityType = entityType,
            EntityId = id.ToString(),
            Action = action,
            Before = before,
            After = after,
        }, cancellationToken);
    }

    private static object MemberSnapshot(EcMember member, AuthorityDirectoryUser? user)
        => new { member.UserId, member.Designation, member.IsActive, UserName = user?.Name, user?.EmployeeId };

    private static object AuthoritySnapshot(RecommendingAuthority authority, AuthorityDirectoryUser? user)
        => new
        {
            authority.Title,
            authority.DepartmentId,
            authority.UserId,
            authority.DefaultActionTagSetId,
            authority.IsActive,
            UserName = user?.Name,
            user?.EmployeeId,
        };

    private static object TagSnapshot(ActionTagSet set)
        => new { set.Name, set.Designation, set.AllowedActions, set.IsActive };

    private static string? NormaliseSearch(string? search)
        => string.IsNullOrWhiteSpace(search) ? null : search.Trim();

    private static void EnsureVersion(long current, long expected)
    {
        if (current != expected)
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }
    }

    private static void AddError(Dictionary<string, string[]> errors, string field, string? message)
    {
        if (message is not null)
        {
            errors[field] = [message];
        }
    }

    private static void ThrowFieldError(string field, string message)
        => throw new EdmException(EdmErrorCodes.ValidationFailed, new Dictionary<string, string[]> { [field] = [message] });

    private static void ThrowIfInvalid(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw new EdmException(EdmErrorCodes.ValidationFailed, errors);
        }
    }
}
