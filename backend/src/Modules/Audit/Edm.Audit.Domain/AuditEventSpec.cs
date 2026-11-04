using System.Text.Json.Nodes;

namespace Edm.Audit.Domain;

/// <summary>
/// Everything needed to create an audit event. <see cref="AuditEvent.Create"/> validates it.
/// The writer fills the id, tenant, time, actor and correlation id from the request context.
/// </summary>
public sealed class AuditEventSpec
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; }

    public Guid? ActorUserId { get; init; }

    public string ActorName { get; init; } = string.Empty;

    public string? ActorDepartment { get; init; }

    public string[] ActorRoles { get; init; } = [];

    public string? SourceIp { get; init; }

    public string? UserAgent { get; init; }

    public string CorrelationId { get; init; } = string.Empty;

    public string Module { get; init; } = string.Empty;

    public string EntityType { get; init; } = string.Empty;

    public string EntityId { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public AuditOutcome Outcome { get; init; } = AuditOutcome.Success;

    public string? FailureReason { get; init; }

    public string? Remarks { get; init; }

    public string? RequestReference { get; init; }

    public int? PolicyVersion { get; init; }

    public JsonNode? Before { get; init; }

    public JsonNode? After { get; init; }
}
