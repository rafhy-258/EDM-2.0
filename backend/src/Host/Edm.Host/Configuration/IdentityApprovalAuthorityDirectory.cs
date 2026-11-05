using Edm.Configuration.Application.Authorities;
using Edm.Configuration.Domain.Authorities;
using Edm.Identity.Application.Organisation;

namespace Edm.Host.Configuration;

public sealed class IdentityApprovalAuthorityDirectory(IOrganisationStore organisationStore) : IApprovalAuthorityDirectory
{
    public async Task<IReadOnlyList<AuthorityDirectoryDepartment>> ListDepartmentsAsync(
        Guid tenantId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var departments = await organisationStore.ListDepartmentsAsync(tenantId, includeInactive, cancellationToken);
        return departments.Select(department =>
            new AuthorityDirectoryDepartment(department.Id, department.Name, department.IsActive)).ToArray();
    }

    public async Task<IReadOnlyList<AuthorityDirectoryUser>> ListUsersAsync(
        Guid tenantId,
        Guid? departmentId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var users = await organisationStore.ListUsersAsync(tenantId, departmentId, includeInactive, cancellationToken);
        var departments = await organisationStore.ListDepartmentsAsync(tenantId, includeInactive: true, cancellationToken);
        var departmentsById = departments.ToDictionary(department => department.Id);
        return users.Select(user =>
        {
            if (!departmentsById.TryGetValue(user.DepartmentId, out var department))
            {
                throw new InvalidOperationException(
                    $"Directory user {user.Id} references missing department {user.DepartmentId}.");
            }

            return new AuthorityDirectoryUser(
                user.Id,
                user.EmployeeId,
                user.Name,
                user.DepartmentId,
                department.Name,
                user.IsActive,
                department.IsActive);
        }).ToArray();
    }

    public async Task<AuthorityDirectoryUser?> FindUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await organisationStore.FindUserAsync(tenantId, userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var department = await organisationStore.FindDepartmentAsync(tenantId, user.DepartmentId, cancellationToken);
        if (department is null)
        {
            throw new InvalidOperationException(
                $"Directory user {user.Id} references missing department {user.DepartmentId}.");
        }

        return new AuthorityDirectoryUser(
            user.Id,
            user.EmployeeId,
            user.Name,
            user.DepartmentId,
            department.Name,
            user.IsActive,
            department.IsActive);
    }
}
