using Edm.Audit.Application;
using Edm.Audit.Domain;
using Edm.SharedKernel.Errors;
using Edm.Web.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Text.Json.Nodes;

namespace Edm.Audit.Api;

/// <summary>Query parameters for the audit list. Bound from the query string with [AsParameters].</summary>
public sealed class AuditListRequest
{
    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }

    public string? Module { get; set; }

    public string? Action { get; set; }

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    public Guid? ActorUserId { get; set; }

    /// <summary>"Success" or "Failure". Compared without regard to case.</summary>
    public string? Outcome { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;

    public AuditQuery ToQuery()
    {
        AuditOutcome? outcome = null;
        if (!string.IsNullOrWhiteSpace(Outcome))
        {
            if (!Enum.TryParse<AuditOutcome>(Outcome, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                throw new EdmException(
                    EdmErrorCodes.ValidationFailed,
                    new Dictionary<string, string[]> { ["outcome"] = ["Outcome must be Success or Failure."] });
            }

            outcome = parsed;
        }

        return new AuditQuery(From, To, Module, Action, EntityType, EntityId, ActorUserId, outcome, Page, PageSize);
    }
}

/// <summary>One audit event as the API returns it. Before, after and changes are JSON values.</summary>
public sealed record AuditEventDto(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    Guid? ActorUserId,
    string ActorName,
    string? ActorDepartment,
    string[] ActorRoles,
    string CorrelationId,
    string Module,
    string EntityType,
    string EntityId,
    string Action,
    string Outcome,
    string? FailureReason,
    string? Remarks,
    string? RequestReference,
    int? PolicyVersion,
    JsonNode? Before,
    JsonNode? After,
    JsonNode? Changes)
{
    public static AuditEventDto From(AuditEvent e) => new(
        e.Id,
        e.OccurredAtUtc,
        e.ActorUserId,
        e.ActorName,
        e.ActorDepartment,
        e.ActorRoles,
        e.CorrelationId,
        e.Module,
        e.EntityType,
        e.EntityId,
        e.Action,
        e.Outcome.ToString(),
        e.FailureReason,
        e.Remarks,
        e.RequestReference,
        e.PolicyVersion,
        Parse(e.BeforeJson),
        Parse(e.AfterJson),
        Parse(e.ChangesJson));

    private static JsonNode? Parse(string? json) => json is null ? null : JsonNode.Parse(json);
}

public sealed record AuditEventPage(IReadOnlyList<AuditEventDto> Items, int Page, int PageSize, int TotalCount);

public static class AuditEndpoints
{
    /// <summary>Maps the audit viewer. Every endpoint declares its permission (Part 20).</summary>
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/audit-events").WithTags("Audit");

        group.MapGet("", async (
                AuditViewer viewer,
                [AsParameters] AuditListRequest request,
                CancellationToken cancellationToken) =>
            {
                var result = await viewer.ListAsync(request.ToQuery(), cancellationToken);
                var page = new AuditEventPage(
                    result.Items.Select(AuditEventDto.From).ToArray(),
                    result.Page,
                    result.PageSize,
                    result.TotalCount);
                return Results.Ok(page);
            })
            .RequiresPermission("audit.view")
            .WithName("ListAuditEvents")
            .WithSummary("List audit events (read-only, paged).");

        return app;
    }
}
