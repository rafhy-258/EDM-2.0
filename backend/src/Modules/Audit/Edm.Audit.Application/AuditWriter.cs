using System.Text.Json;
using System.Text.Json.Nodes;
using Edm.Audit.Domain;
using Edm.SharedKernel.Context;

namespace Edm.Audit.Application;

/// <summary>Serialisation options for audit snapshots. camelCase, matching the API.</summary>
internal static class AuditJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

/// <summary>Fills in the time, tenant, actor and correlation id, then appends the event through the store.</summary>
public sealed class AuditWriter(
    IAuditStore store,
    IClock clock,
    ITenantContext tenant,
    IActorContext actor,
    ICorrelationIdAccessor correlation) : IAuditWriter
{
    public async Task WriteAsync(AuditEventDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var spec = new AuditEventSpec
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.TenantId,
            OccurredAtUtc = clock.UtcNow.ToUniversalTime(),
            ActorUserId = actor.UserId,
            ActorName = actor.DisplayName,
            ActorDepartment = actor.Department,
            ActorRoles = actor.Roles.ToArray(),
            SourceIp = actor.SourceIp,
            UserAgent = actor.UserAgent,
            CorrelationId = correlation.CorrelationId,
            Module = draft.Module,
            EntityType = draft.EntityType,
            EntityId = draft.EntityId,
            Action = draft.Action,
            Outcome = draft.Outcome,
            FailureReason = draft.FailureReason,
            Remarks = draft.Remarks,
            RequestReference = draft.RequestReference,
            PolicyVersion = draft.PolicyVersion,
            Before = ToNode(draft.Before),
            After = ToNode(draft.After),
        };

        await store.AppendAsync(AuditEvent.Create(spec), cancellationToken);
    }

    private static JsonNode? ToNode(object? value)
        => value is null ? null : JsonSerializer.SerializeToNode(value, AuditJson.Options);
}
