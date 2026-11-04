using Edm.Audit.Domain;

namespace Edm.Audit.Application;

/// <summary>What a module knows about an action. The writer adds the time, tenant, actor and correlation id.</summary>
public sealed record AuditEventDraft
{
    public required string Module { get; init; }

    public required string EntityType { get; init; }

    public required string EntityId { get; init; }

    public required string Action { get; init; }

    public AuditOutcome Outcome { get; init; } = AuditOutcome.Success;

    public string? FailureReason { get; init; }

    public string? Remarks { get; init; }

    public string? RequestReference { get; init; }

    public int? PolicyVersion { get; init; }

    /// <summary>State before the action, serialised as JSON. Null for a create.</summary>
    public object? Before { get; init; }

    /// <summary>State after the action, serialised as JSON. Null for a delete.</summary>
    public object? After { get; init; }
}

/// <summary>Filters and paging for the audit viewer (Part 10.4).</summary>
public sealed record AuditQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Module,
    string? Action,
    string? EntityType,
    string? EntityId,
    Guid? ActorUserId,
    AuditOutcome? Outcome,
    int Page,
    int PageSize);
