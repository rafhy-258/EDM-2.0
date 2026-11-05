using Edm.Identity.Application.Organisation;
using Edm.Identity.Domain.Organisation;
using Edm.Web.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Edm.Identity.Api;

/// <summary>Request body for creating a department.</summary>
public sealed record DepartmentCreateRequest(string? Name);

/// <summary>Request body for editing a department, or deactivating and reactivating it. Needs the current row version.</summary>
public sealed record DepartmentUpdateRequest(string? Name, bool? IsActive, long RowVersion);

/// <summary>Request body for adding a person to the directory.</summary>
public sealed record DirectoryUserCreateRequest(string? EmployeeId, string? Name, string? Email, Guid? DepartmentId);

/// <summary>Request body for editing a person, or deactivating and reactivating them. Needs the current row version.</summary>
public sealed record DirectoryUserUpdateRequest(
    string? EmployeeId,
    string? Name,
    string? Email,
    Guid? DepartmentId,
    bool? IsActive,
    long RowVersion);

/// <summary>An active department for pickers: id and name only.</summary>
public sealed record DepartmentOption(Guid Id, string Name);

/// <summary>One department as returned to the client.</summary>
public sealed record DepartmentResponse(
    Guid Id,
    string Name,
    bool IsActive,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy)
{
    public static DepartmentResponse From(Department department)
        => new(department.Id, department.Name, department.IsActive, department.RowVersion, department.UpdatedAtUtc, department.UpdatedBy);
}

/// <summary>One directory person as returned to the client.</summary>
public sealed record DirectoryUserResponse(
    Guid Id,
    string EmployeeId,
    string Name,
    string? Email,
    Guid DepartmentId,
    bool IsActive,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy)
{
    public static DirectoryUserResponse From(DirectoryUser user)
        => new(
            user.Id,
            user.EmployeeId,
            user.Name,
            user.Email,
            user.DepartmentId,
            user.IsActive,
            user.RowVersion,
            user.UpdatedAtUtc,
            user.UpdatedBy);
}

/// <summary>
/// Organisation endpoints (Master Prompt Part 21.2, Phase 0 directory). Departments need org.department.manage (there is
/// no view-only department permission). Reading people needs org.user.view; changing them needs org.user.manage.
/// </summary>
public static class OrganisationEndpoints
{
    public static IEndpointRouteBuilder MapOrganisationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Organisation");

        group.MapGet("/departments", async Task<IResult> (bool includeInactive, DepartmentService service, CancellationToken cancellationToken) =>
            {
                var rows = await service.ListAsync(includeInactive, cancellationToken);
                return Results.Ok(rows.Select(DepartmentResponse.From).ToArray());
            })
            .RequiresPermission("org.department.manage")
            .WithName("ListDepartments")
            .WithSummary("List departments. Inactive ones are included only when asked.");

        // Active departments only, id and name only. Lets people-directory viewers show and pick departments
        // without the department management permission.
        group.MapGet("/departments/options", async Task<IResult> (DepartmentService service, CancellationToken cancellationToken) =>
            {
                var rows = await service.ListAsync(includeInactive: false, cancellationToken);
                return Results.Ok(rows.Select(row => new DepartmentOption(row.Id, row.Name)).ToArray());
            })
            .RequiresPermission("org.user.view")
            .WithName("ListDepartmentOptions")
            .WithSummary("Active departments as id and name, for directory screens.");

        group.MapPost("/departments", async Task<IResult> (DepartmentCreateRequest body, DepartmentService service, CancellationToken cancellationToken) =>
            {
                var created = await service.CreateAsync(new DepartmentCreate(body.Name), cancellationToken);
                var response = DepartmentResponse.From(created);
                return Results.Created($"/api/v1/departments/{response.Id}", response);
            })
            .RequiresPermission("org.department.manage")
            .WithName("CreateDepartment")
            .WithSummary("Create a department. The name must be unique among active departments.");

        group.MapPut("/departments/{id:guid}", async Task<IResult> (Guid id, DepartmentUpdateRequest body, DepartmentService service, CancellationToken cancellationToken) =>
            {
                var updated = await service.UpdateAsync(id, new DepartmentUpdate(body.Name, body.IsActive, body.RowVersion), cancellationToken);
                return updated is null ? Results.NotFound() : Results.Ok(DepartmentResponse.From(updated));
            })
            .RequiresPermission("org.department.manage")
            .WithName("UpdateDepartment")
            .WithSummary("Rename, deactivate or reactivate a department. Needs the current row version.");

        group.MapGet("/users", async Task<IResult> (Guid? departmentId, bool includeInactive, DirectoryUserService service, CancellationToken cancellationToken) =>
            {
                var rows = await service.ListAsync(departmentId, includeInactive, cancellationToken);
                return Results.Ok(rows.Select(DirectoryUserResponse.From).ToArray());
            })
            .RequiresPermission("org.user.view")
            .WithName("ListDirectoryUsers")
            .WithSummary("List people in the organisation directory, optionally for one department.");

        group.MapPost("/users", async Task<IResult> (DirectoryUserCreateRequest body, DirectoryUserService service, CancellationToken cancellationToken) =>
            {
                var created = await service.CreateAsync(
                    new DirectoryUserCreate(body.EmployeeId, body.Name, body.Email, body.DepartmentId),
                    cancellationToken);
                var response = DirectoryUserResponse.From(created);
                return Results.Created($"/api/v1/users/{response.Id}", response);
            })
            .RequiresPermission("org.user.manage")
            .WithName("CreateDirectoryUser")
            .WithSummary("Add a person to the directory in an active department.");

        group.MapPut("/users/{id:guid}", async Task<IResult> (Guid id, DirectoryUserUpdateRequest body, DirectoryUserService service, CancellationToken cancellationToken) =>
            {
                var updated = await service.UpdateAsync(
                    id,
                    new DirectoryUserUpdate(body.EmployeeId, body.Name, body.Email, body.DepartmentId, body.IsActive, body.RowVersion),
                    cancellationToken);
                return updated is null ? Results.NotFound() : Results.Ok(DirectoryUserResponse.From(updated));
            })
            .RequiresPermission("org.user.manage")
            .WithName("UpdateDirectoryUser")
            .WithSummary("Edit, deactivate or reactivate a person. Needs the current row version.");

        return app;
    }
}
