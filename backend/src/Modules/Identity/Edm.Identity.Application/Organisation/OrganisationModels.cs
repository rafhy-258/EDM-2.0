using Edm.Identity.Domain.Organisation;

namespace Edm.Identity.Application.Organisation;

public sealed record DepartmentCreate(string? Name);

/// <summary>Null IsActive keeps the current state. RowVersion is the version the user read.</summary>
public sealed record DepartmentUpdate(string? Name, bool? IsActive, long RowVersion);

public sealed record DirectoryUserCreate(string? EmployeeId, string? Name, string? Email, Guid? DepartmentId);

/// <summary>Null fields keep their current values. Null IsActive keeps the current state.</summary>
public sealed record DirectoryUserUpdate(
    string? EmployeeId,
    string? Name,
    string? Email,
    Guid? DepartmentId,
    bool? IsActive,
    long RowVersion);

/// <summary>Persistence for departments and directory users. Every statement filters by tenant.</summary>
public interface IOrganisationStore
{
    Task<IReadOnlyList<Department>> ListDepartmentsAsync(Guid tenantId, bool includeInactive, CancellationToken cancellationToken);

    Task<Department?> FindDepartmentAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>True when another active department already has this name (ignoring case).</summary>
    Task<bool> DepartmentNameTakenAsync(Guid tenantId, string name, Guid exceptId, CancellationToken cancellationToken);

    Task<int> CountActiveUsersInDepartmentAsync(Guid tenantId, Guid departmentId, CancellationToken cancellationToken);

    Task InsertDepartmentAsync(Department department, CancellationToken cancellationToken);

    /// <summary>False when the stored row version is no longer expectedVersion.</summary>
    Task<bool> UpdateDepartmentAsync(Department department, long expectedVersion, CancellationToken cancellationToken);

    Task<IReadOnlyList<DirectoryUser>> ListUsersAsync(
        Guid tenantId,
        Guid? departmentId,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<DirectoryUser?> FindUserAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>True when any row (active or not) has this employee ID, ignoring case, other than exceptId.</summary>
    Task<bool> EmployeeIdTakenAsync(Guid tenantId, string employeeId, Guid exceptId, CancellationToken cancellationToken);

    Task InsertUserAsync(DirectoryUser user, CancellationToken cancellationToken);

    /// <summary>False when the stored row version is no longer expectedVersion.</summary>
    Task<bool> UpdateUserAsync(DirectoryUser user, long expectedVersion, CancellationToken cancellationToken);
}
