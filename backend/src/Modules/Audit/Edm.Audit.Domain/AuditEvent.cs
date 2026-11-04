using System.Text.Json.Nodes;
using Edm.SharedKernel.Errors;

namespace Edm.Audit.Domain;

/// <summary>
/// An immutable audit event (ADR-0005). Once created, no property changes. The database rejects
/// UPDATE, DELETE and TRUNCATE on the table as a second line of defence.
/// </summary>
public sealed class AuditEvent
{
    // Private constructor for EF Core materialisation. Application code uses Create.
    private AuditEvent()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public string ActorName { get; private set; } = string.Empty;

    public string? ActorDepartment { get; private set; }

    public string[] ActorRoles { get; private set; } = [];

    public string? SourceIp { get; private set; }

    public string? UserAgent { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public string Module { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string EntityId { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public AuditOutcome Outcome { get; private set; }

    public string? FailureReason { get; private set; }

    public string? Remarks { get; private set; }

    public string? RequestReference { get; private set; }

    public int? PolicyVersion { get; private set; }

    public string? BeforeJson { get; private set; }

    public string? AfterJson { get; private set; }

    /// <summary>JSON array of field-level changes: each item has "field", "before" and "after".</summary>
    public string? ChangesJson { get; private set; }

    /// <summary>
    /// Validates the spec and builds the event. Throws <see cref="EdmException"/> with field errors
    /// when a required value is missing, a value is too long, or a failure has no reason.
    /// </summary>
    public static AuditEvent Create(AuditEventSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (spec.Id == Guid.Empty)
        {
            errors["id"] = ["An event id is required."];
        }

        if (spec.TenantId == Guid.Empty)
        {
            errors["tenantId"] = ["A tenant is required."];
        }

        if (spec.OccurredAtUtc == default)
        {
            errors["occurredAtUtc"] = ["The event time is required."];
        }

        RequireText(errors, "actorName", spec.ActorName, 200);
        RequireText(errors, "correlationId", spec.CorrelationId, 128);
        RequireText(errors, "module", spec.Module, 64);
        RequireText(errors, "entityType", spec.EntityType, 100);
        RequireText(errors, "entityId", spec.EntityId, 200);
        RequireText(errors, "action", spec.Action, 100);
        LimitText(errors, "actorDepartment", spec.ActorDepartment, 200);
        LimitText(errors, "sourceIp", spec.SourceIp, 64);
        LimitText(errors, "userAgent", spec.UserAgent, 512);
        LimitText(errors, "remarks", spec.Remarks, 1000);
        LimitText(errors, "failureReason", spec.FailureReason, 500);
        LimitText(errors, "requestReference", spec.RequestReference, 64);

        if (spec.Outcome == AuditOutcome.Failure && string.IsNullOrWhiteSpace(spec.FailureReason))
        {
            errors["failureReason"] = ["A failed action needs a failure reason."];
        }

        if (spec.PolicyVersion is < 1)
        {
            errors["policyVersion"] = ["The policy version must be 1 or greater."];
        }

        if (errors.Count > 0)
        {
            throw new EdmException(EdmErrorCodes.ValidationFailed, errors);
        }

        var changes = spec.Before is null && spec.After is null
            ? null
            : ToChangesJson(AuditDiff.Compute(spec.Before, spec.After));

        return new AuditEvent
        {
            Id = spec.Id,
            TenantId = spec.TenantId,
            OccurredAtUtc = spec.OccurredAtUtc.ToUniversalTime(),
            ActorUserId = spec.ActorUserId,
            ActorName = spec.ActorName,
            ActorDepartment = spec.ActorDepartment,
            ActorRoles = spec.ActorRoles,
            SourceIp = spec.SourceIp,
            UserAgent = spec.UserAgent,
            CorrelationId = spec.CorrelationId,
            Module = spec.Module,
            EntityType = spec.EntityType,
            EntityId = spec.EntityId,
            Action = spec.Action,
            Outcome = spec.Outcome,
            FailureReason = spec.FailureReason,
            Remarks = spec.Remarks,
            RequestReference = spec.RequestReference,
            PolicyVersion = spec.PolicyVersion,
            BeforeJson = spec.Before?.ToJsonString(),
            AfterJson = spec.After?.ToJsonString(),
            ChangesJson = changes,
        };
    }

    private static void RequireText(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required."];
            return;
        }

        LimitText(errors, field, value, maxLength);
    }

    private static void LimitText(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (value is not null && value.Length > maxLength)
        {
            errors[field] = [$"{field} must be {maxLength} characters or fewer."];
        }
    }

    private static string ToChangesJson(IReadOnlyList<FieldChange> changes)
    {
        var array = new JsonArray();
        foreach (var change in changes)
        {
            array.Add(new JsonObject
            {
                ["field"] = JsonValue.Create(change.Field),
                ["before"] = change.Before,
                ["after"] = change.After,
            });
        }

        return array.ToJsonString();
    }
}
