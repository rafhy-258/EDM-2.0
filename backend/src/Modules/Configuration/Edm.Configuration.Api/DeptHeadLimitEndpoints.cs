using Edm.Configuration.Application.Limits;
using Edm.Configuration.Domain.Limits;
using Edm.Web.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Edm.Configuration.Api;

/// <summary>Request body for saving a department-head limit. Null RowVersion means no limit is active yet.</summary>
public sealed record DeptHeadLimitSaveRequest(
    string? Nature,
    decimal? AmountPkr,
    string? Description,
    DateOnly? EffectiveDate,
    long? RowVersion);

/// <summary>Request body for ending the active department-head limit.</summary>
public sealed record DeptHeadLimitDeactivateRequest(string? Nature, long RowVersion);

/// <summary>One limit row as returned to the client.</summary>
public sealed record DeptHeadLimitResponse(
    Guid Id,
    string Nature,
    decimal AmountPkr,
    string? Description,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    bool IsActive,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy)
{
    public static DeptHeadLimitResponse From(DeptHeadLimit limit)
        => new(
            limit.Id,
            LimitNatureCodes.Code(limit.Nature),
            limit.AmountPkr,
            limit.Description,
            limit.EffectiveFrom,
            limit.EffectiveTo,
            limit.IsActive,
            limit.RowVersion,
            limit.UpdatedAtUtc,
            limit.UpdatedBy);
}

/// <summary>What applies now for one nature, with the active row (if any) and the history.</summary>
public sealed record DeptHeadLimitSummaryResponse(
    string Nature,
    decimal CurrentAmountPkr,
    bool IsDefault,
    string Status,
    decimal DefaultAmountPkr,
    DeptHeadLimitResponse? Active,
    DateTimeOffset? LastChangedAtUtc,
    IReadOnlyList<DeptHeadLimitResponse> History)
{
    public static DeptHeadLimitSummaryResponse From(DeptHeadLimitSummary summary)
        => new(
            LimitNatureCodes.Code(summary.Nature),
            summary.Status.CurrentAmountPkr,
            summary.Status.IsDefault,
            summary.Status.Status,
            DeptHeadLimitRules.DefaultAmountPkr,
            summary.Status.Open is null ? null : DeptHeadLimitResponse.From(summary.Status.Open),
            summary.Status.LastChangedAtUtc,
            summary.History.Select(DeptHeadLimitResponse.From).ToArray());
}

/// <summary>
/// Department-head limit endpoints (Master Prompt Part 21.2, group Limits). All need config.limit.manage (ADR-0007).
/// </summary>
public static class DeptHeadLimitEndpoints
{
    public static IEndpointRouteBuilder MapDeptHeadLimitEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/dept-head-limits", async Task<IResult> (string? nature, DeptHeadLimitService service, CancellationToken cancellationToken) =>
            {
                var summary = await service.GetSummaryAsync(nature, cancellationToken);
                return Results.Ok(DeptHeadLimitSummaryResponse.From(summary));
            })
            .RequiresPermission("config.limit.manage")
            .WithName("GetDeptHeadLimit")
            .WithSummary("Show the department-head limit for OPEX or CAPEX: what applies now, and the history.");

        app.MapPost("/dept-head-limits", async Task<IResult> (DeptHeadLimitSaveRequest body, DeptHeadLimitService service, CancellationToken cancellationToken) =>
            {
                var created = await service.SaveAsync(
                    new DeptHeadLimitSave(body.Nature, body.AmountPkr, body.Description, body.EffectiveDate, body.RowVersion),
                    cancellationToken);
                var response = DeptHeadLimitResponse.From(created);
                return Results.Created($"/api/v1/dept-head-limits/{response.Id}", response);
            })
            .RequiresPermission("config.limit.manage")
            .WithName("SaveDeptHeadLimit")
            .WithSummary("Set a new limit. The active limit, if any, ends on the new effective date and is kept as history.");

        app.MapPost("/dept-head-limits/deactivate", async Task<IResult> (DeptHeadLimitDeactivateRequest body, DeptHeadLimitService service, CancellationToken cancellationToken) =>
            {
                var deactivated = await service.DeactivateAsync(body.Nature, body.RowVersion, cancellationToken);
                return deactivated ? Results.NoContent() : Results.NotFound();
            })
            .RequiresPermission("config.limit.manage")
            .WithName("DeactivateDeptHeadLimit")
            .WithSummary("End the active limit now. The seeded default applies until a new limit is set.");

        return app;
    }
}
