using Edm.Configuration.Application.Authorities;
using Edm.Configuration.Domain.Authorities;
using Edm.Web.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Edm.Configuration.Api;

public sealed record EcMemberCreateRequest(Guid? UserId, string? Designation);
public sealed record EcMemberUpdateRequest(bool? IsActive, long RowVersion);
public sealed record RecommendingAuthorityCreateRequest(string? Title, Guid? DepartmentId, Guid? UserId);
public sealed record RecommendingAuthorityUpdateRequest(Guid? DepartmentId, Guid? UserId, bool? IsActive, long RowVersion);
public sealed record ActionTagSetUpdateRequest(string[]? AllowedActions, long RowVersion);

public sealed record ApprovalDirectoryUserResponse(
    Guid Id,
    string EmployeeId,
    string Name,
    Guid DepartmentId,
    string DepartmentName);
public sealed record ApprovalDirectoryDepartmentResponse(Guid Id, string Name);

public sealed record EcMemberResponse(
    Guid Id,
    Guid UserId,
    string EmployeeId,
    string UserName,
    Guid DepartmentId,
    string DepartmentName,
    string Designation,
    string DesignationLabel,
    bool IsActive,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy)
{
    public static EcMemberResponse From(EcMemberView view)
        => new(
            view.Member.Id,
            view.Member.UserId,
            view.User.EmployeeId,
            view.User.Name,
            view.User.DepartmentId,
            view.User.DepartmentName,
            view.Member.Designation,
            ApprovalDesignationCodes.Label(view.Member.Designation),
            view.Member.IsActive,
            view.Member.RowVersion,
            view.Member.UpdatedAtUtc,
            view.Member.UpdatedBy);
}

public sealed record RecommendingAuthorityResponse(
    Guid Id,
    string Title,
    Guid DepartmentId,
    string DepartmentName,
    Guid UserId,
    string EmployeeId,
    string UserName,
    Guid DefaultActionTagSetId,
    string ActionTagSetName,
    bool IsActive,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy)
{
    public static RecommendingAuthorityResponse From(RecommendingAuthorityView view)
        => new(
            view.Authority.Id,
            view.Authority.Title,
            view.Authority.DepartmentId,
            view.User.DepartmentName,
            view.Authority.UserId,
            view.User.EmployeeId,
            view.User.Name,
            view.Authority.DefaultActionTagSetId,
            view.ActionTagSetName,
            view.Authority.IsActive,
            view.Authority.RowVersion,
            view.Authority.UpdatedAtUtc,
            view.Authority.UpdatedBy);
}

public sealed record ActionTagSetResponse(
    Guid Id,
    string Name,
    string Designation,
    string DesignationLabel,
    string[] AllowedActions,
    bool IsActive,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy)
{
    public static ActionTagSetResponse From(Edm.Configuration.Domain.Authorities.ActionTagSet set)
        => new(
            set.Id,
            set.Name,
            set.Designation,
            ApprovalDesignationCodes.Label(set.Designation),
            set.AllowedActions.ToArray(),
            set.IsActive,
            set.RowVersion,
            set.UpdatedAtUtc,
            set.UpdatedBy);
}

public static class ApprovalAuthorityEndpoints
{
    public static RouteGroupBuilder MapApprovalAuthorityEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/ec-members", async Task<IResult> (
                [FromQuery] string? designation,
                [FromQuery] bool? includeInactive,
                [FromQuery] string? search,
                ApprovalAuthorityService service,
                CancellationToken cancellationToken) =>
            Results.Ok((await service.ListMembersAsync(
                designation, includeInactive ?? false, search, cancellationToken))
                .Select(EcMemberResponse.From).ToArray()))
            .RequiresPermission("config.ec.pool.manage")
            .WithName("ListEcMembers")
            .WithSummary("List EC pool members, optionally including inactive records.");

        group.MapGet("/ec-members/candidates", async Task<IResult> (
                [FromQuery] Guid? departmentId,
                ApprovalAuthorityService service,
                CancellationToken cancellationToken) =>
            Results.Ok((await service.ListCandidatesAsync(departmentId, cancellationToken))
                .Select(ApprovalDirectoryUserResponseFrom).ToArray()))
            .RequiresPermission("config.ec.pool.manage")
            .WithName("ListEcMemberCandidates")
            .WithSummary("List active organisation users available for the EC member pool.");

        group.MapPost("/ec-members", async Task<IResult> (
                EcMemberCreateRequest body,
                ApprovalAuthorityService service,
                CancellationToken cancellationToken) =>
            {
                var member = await service.CreateMemberAsync(
                    new EcMemberCreate(body.UserId, body.Designation), cancellationToken);
                var response = EcMemberResponse.From(new EcMemberView(
                    member,
                    await service.FindDirectoryUserAsync(member.UserId, cancellationToken)));
                return Results.Created($"/api/v1/ec-members/{response.Id}", response);
            })
            .RequiresPermission("config.ec.pool.manage")
            .WithName("CreateEcMember")
            .WithSummary("Add an active directory user to the EC member pool.");

        group.MapPut("/ec-members/{id:guid}", async Task<IResult> (
                Guid id,
                EcMemberUpdateRequest body,
                ApprovalAuthorityService service,
                CancellationToken cancellationToken) =>
            {
                var member = await service.UpdateMemberAsync(
                    id, new EcMemberChange(body.IsActive, body.RowVersion), cancellationToken);
                if (member is null)
                {
                    return Results.NotFound();
                }

                var user = await service.FindDirectoryUserAsync(member.UserId, cancellationToken);
                return Results.Ok(EcMemberResponse.From(new EcMemberView(member, user)));
            })
            .RequiresPermission("config.ec.pool.manage")
            .WithName("UpdateEcMember")
            .WithSummary("Activate or deactivate an EC pool member.");

        group.MapDelete("/ec-members/{id:guid}", async Task<IResult> (
                Guid id,
                [FromQuery] long rowVersion,
                ApprovalAuthorityService service,
                CancellationToken cancellationToken) =>
            await service.DeleteMemberAsync(id, rowVersion, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound())
            .RequiresPermission("config.ec.pool.manage")
            .WithName("DeleteEcMember")
            .WithSummary("Remove an unused EC pool member.");

        group.MapGet("/recommending-authorities", async Task<IResult> (
                [FromQuery] bool? includeInactive,
                [FromQuery] string? search,
                ApprovalAuthorityService service,
                CancellationToken cancellationToken) =>
            Results.Ok((await service.ListAuthoritiesAsync(includeInactive ?? false, search, cancellationToken))
                .Select(RecommendingAuthorityResponse.From).ToArray()))
            .RequiresPermission("config.ra.manage")
            .WithName("ListRecommendingAuthorities")
            .WithSummary("List recommending authorities.");

        group.MapGet("/recommending-authorities/candidates", async Task<IResult> (
                [FromQuery] Guid? departmentId,
                ApprovalAuthorityService service,
                CancellationToken cancellationToken) =>
            Results.Ok((await service.ListCandidatesAsync(departmentId, cancellationToken))
                .Select(ApprovalDirectoryUserResponseFrom).ToArray()))
            .RequiresPermission("config.ra.manage")
            .WithName("ListRecommendingAuthorityCandidates")
            .WithSummary("List active users in the selected department.");

        group.MapGet("/recommending-authorities/departments", async Task<IResult> (
                ApprovalAuthorityService service,
                CancellationToken cancellationToken) =>
            Results.Ok((await service.ListDepartmentsAsync(cancellationToken))
                .Select(department => new ApprovalDirectoryDepartmentResponse(department.Id, department.Name)).ToArray()))
            .RequiresPermission("config.ra.manage")
            .WithName("ListRecommendingAuthorityDepartments")
            .WithSummary("List active departments available for recommending-authority assignments.");

        group.MapPost("/recommending-authorities", async Task<IResult> (
                RecommendingAuthorityCreateRequest body,
                ApprovalAuthorityService service,
                CancellationToken cancellationToken) =>
            {
                var authority = await service.CreateAuthorityAsync(
                    new RecommendingAuthorityCreate(body.Title, body.DepartmentId, body.UserId), cancellationToken);
                var user = await service.FindDirectoryUserAsync(authority.UserId, cancellationToken);
                var tag = await service.FindActionTagSetNameAsync(authority.DefaultActionTagSetId, cancellationToken);
                var response = RecommendingAuthorityResponse.From(new RecommendingAuthorityView(authority, user, tag));
                return Results.Created($"/api/v1/recommending-authorities/{response.Id}", response);
            })
            .RequiresPermission("config.ra.manage")
            .WithName("CreateRecommendingAuthority")
            .WithSummary("Create an authority for a department user.");

        group.MapPut("/recommending-authorities/{id:guid}", async Task<IResult> (
                Guid id,
                RecommendingAuthorityUpdateRequest body,
                ApprovalAuthorityService service,
                CancellationToken cancellationToken) =>
            {
                var authority = await service.UpdateAuthorityAsync(
                    id,
                    new RecommendingAuthorityChange(body.DepartmentId, body.UserId, body.IsActive, body.RowVersion),
                    cancellationToken);
                if (authority is null)
                {
                    return Results.NotFound();
                }

                var user = await service.FindDirectoryUserAsync(authority.UserId, cancellationToken);
                var tag = await service.FindActionTagSetNameAsync(authority.DefaultActionTagSetId, cancellationToken);
                return Results.Ok(RecommendingAuthorityResponse.From(
                    new RecommendingAuthorityView(authority, user, tag)));
            })
            .RequiresPermission("config.ra.manage")
            .WithName("UpdateRecommendingAuthority")
            .WithSummary("Change the assigned department user or activate the authority. Titles are immutable.");

        group.MapDelete("/recommending-authorities/{id:guid}", async Task<IResult> (
                Guid id,
                [FromQuery] long rowVersion,
                ApprovalAuthorityService service,
                CancellationToken cancellationToken) =>
            await service.DeleteAuthorityAsync(id, rowVersion, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound())
            .RequiresPermission("config.ra.manage")
            .WithName("DeleteRecommendingAuthority")
            .WithSummary("Remove an unused recommending authority.");

        group.MapGet("/action-tag-sets", async Task<IResult> (
                ApprovalAuthorityService service,
                CancellationToken cancellationToken) =>
            Results.Ok((await service.ListActionTagSetsAsync(cancellationToken))
                .Select(ActionTagSetResponse.From).ToArray()))
            .RequiresPermission("config.actiontags.manage")
            .WithName("ListActionTagSets")
            .WithSummary("List configurable action defaults for approval designations.");

        group.MapPut("/action-tag-sets/{id:guid}", async Task<IResult> (
                Guid id,
                ActionTagSetUpdateRequest body,
                ApprovalAuthorityService service,
                CancellationToken cancellationToken) =>
            {
                var set = await service.UpdateActionTagSetAsync(
                    id, new ActionTagSetChange(body.AllowedActions, body.RowVersion), cancellationToken);
                return set is null ? Results.NotFound() : Results.Ok(ActionTagSetResponse.From(set));
            })
            .RequiresPermission("config.actiontags.manage")
            .WithName("UpdateActionTagSet")
            .WithSummary("Update actions available by designation. Approve is required and EC members cannot forward.");

        return group;
    }

    private static ApprovalDirectoryUserResponse ApprovalDirectoryUserResponseFrom(AuthorityDirectoryUser user)
        => new(user.Id, user.EmployeeId, user.Name, user.DepartmentId, user.DepartmentName);
}
