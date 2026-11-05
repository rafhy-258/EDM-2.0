using Edm.Identity.Application.Organisation;
using Edm.Identity.Domain.Organisation;

namespace Edm.UnitTests.Identity;

/// <summary>In-memory store with the same rules as the PostgreSQL store: active-name and employee-ID uniqueness, and row versions.</summary>
internal sealed class InMemoryOrganisationStore : IOrganisationStore
{
    public List<Department> Departments { get; } = new();

    public List<DirectoryUser> Users { get; } = new();

    public Task<IReadOnlyList<Department>> ListDepartmentsAsync(Guid tenantId, bool includeInactive, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Department>>(
            Departments.Where(d => d.TenantId == tenantId && (includeInactive || d.IsActive))
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .ToList());

    public Task<Department?> FindDepartmentAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(Departments.FirstOrDefault(d => d.TenantId == tenantId && d.Id == id));

    public Task<bool> DepartmentNameTakenAsync(Guid tenantId, string name, Guid exceptId, CancellationToken cancellationToken)
        => Task.FromResult(Departments.Any(d =>
            d.TenantId == tenantId && d.IsActive && d.Id != exceptId
            && string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)));

    public Task<int> CountActiveUsersInDepartmentAsync(Guid tenantId, Guid departmentId, CancellationToken cancellationToken)
        => Task.FromResult(Users.Count(u => u.TenantId == tenantId && u.DepartmentId == departmentId && u.IsActive));

    public Task InsertDepartmentAsync(Department department, CancellationToken cancellationToken)
    {
        Departments.Add(department);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateDepartmentAsync(Department department, long expectedVersion, CancellationToken cancellationToken)
    {
        var index = Departments.FindIndex(d => d.Id == department.Id && d.TenantId == department.TenantId && d.RowVersion == expectedVersion);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        Departments[index] = department;
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<DirectoryUser>> ListUsersAsync(
        Guid tenantId,
        Guid? departmentId,
        bool includeInactive,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<DirectoryUser>>(
            Users.Where(u => u.TenantId == tenantId
                && (departmentId is null || u.DepartmentId == departmentId.Value)
                && (includeInactive || u.IsActive))
                .OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
                .ToList());

    public Task<DirectoryUser?> FindUserAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(Users.FirstOrDefault(u => u.TenantId == tenantId && u.Id == id));

    public Task<bool> EmployeeIdTakenAsync(Guid tenantId, string employeeId, Guid exceptId, CancellationToken cancellationToken)
        => Task.FromResult(Users.Any(u =>
            u.TenantId == tenantId && u.Id != exceptId
            && string.Equals(u.EmployeeId, employeeId, StringComparison.OrdinalIgnoreCase)));

    public Task InsertUserAsync(DirectoryUser user, CancellationToken cancellationToken)
    {
        Users.Add(user);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateUserAsync(DirectoryUser user, long expectedVersion, CancellationToken cancellationToken)
    {
        var index = Users.FindIndex(u => u.Id == user.Id && u.TenantId == user.TenantId && u.RowVersion == expectedVersion);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        Users[index] = user;
        return Task.FromResult(true);
    }
}
