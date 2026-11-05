using Edm.Configuration.Domain.Authorities;

namespace Edm.Configuration.Application.Authorities;

public sealed record EcMemberCreate(Guid? UserId, string? Designation);

public sealed record EcMemberChange(bool? IsActive, long RowVersion);

public sealed record RecommendingAuthorityCreate(string? Title, Guid? DepartmentId, Guid? UserId);

public sealed record RecommendingAuthorityChange(Guid? DepartmentId, Guid? UserId, bool? IsActive, long RowVersion);

public sealed record ActionTagSetChange(IReadOnlyList<string>? AllowedActions, long RowVersion);

public sealed record AuthorityDirectoryDepartment(Guid Id, string Name, bool IsActive);

public interface IApprovalAuthorityStore
{
    Task EnsureDefaultActionTagSetsAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActionTagSet>> ListActionTagSetsAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<ActionTagSet?> FindActionTagSetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<bool> UpdateActionTagSetAsync(ActionTagSet set, long expectedVersion, CancellationToken cancellationToken);

    Task<IReadOnlyList<EcMember>> ListEcMembersAsync(
        Guid tenantId,
        string? designation,
        bool includeInactive,
        string? search,
        CancellationToken cancellationToken);
    Task<EcMember?> FindEcMemberAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<EcMember?> FindEcMemberAsync(Guid tenantId, Guid userId, string designation, CancellationToken cancellationToken);
    Task<bool> HasActiveHolderAsync(Guid tenantId, string designation, CancellationToken cancellationToken);
    Task InsertEcMemberAsync(EcMember member, CancellationToken cancellationToken);
    Task<bool> UpdateEcMemberAsync(EcMember member, long expectedVersion, CancellationToken cancellationToken);
    Task<bool> SoftDeleteEcMemberAsync(Guid tenantId, Guid id, long expectedVersion, Guid? deletedBy, DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyList<RecommendingAuthority>> ListRecommendingAuthoritiesAsync(
        Guid tenantId,
        bool includeInactive,
        string? search,
        CancellationToken cancellationToken);
    Task<RecommendingAuthority?> FindRecommendingAuthorityAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<bool> RecommendingAuthorityTakenAsync(Guid tenantId, Guid departmentId, Guid userId, Guid exceptId, CancellationToken cancellationToken);
    Task<Guid?> FindActionTagSetIdAsync(Guid tenantId, string designation, CancellationToken cancellationToken);
    Task InsertRecommendingAuthorityAsync(RecommendingAuthority authority, CancellationToken cancellationToken);
    Task<bool> UpdateRecommendingAuthorityAsync(RecommendingAuthority authority, long expectedVersion, CancellationToken cancellationToken);
    Task<bool> SoftDeleteRecommendingAuthorityAsync(Guid tenantId, Guid id, long expectedVersion, Guid? deletedBy, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>Configuration-owned port. The host adapts this to the Identity application module.</summary>
public interface IApprovalAuthorityDirectory
{
    Task<IReadOnlyList<AuthorityDirectoryDepartment>> ListDepartmentsAsync(
        Guid tenantId,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AuthorityDirectoryUser>> ListUsersAsync(
        Guid tenantId,
        Guid? departmentId,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<AuthorityDirectoryUser?> FindUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
}
