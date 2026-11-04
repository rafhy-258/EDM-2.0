using Edm.Audit.Domain;
using Edm.SharedKernel.Errors;
using Edm.SharedKernel.Paging;

namespace Edm.Audit.Application;

/// <summary>
/// Read-only access to the audit store (Part 10.4). Every view is itself an audit event (Part 10.2).
/// </summary>
public sealed class AuditViewer(IAuditStore store, IAuditWriter writer)
{
    public const int MaxPageSize = 200;

    public async Task<PagedResult<AuditEvent>> ListAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        Validate(query);

        var result = await store.QueryAsync(query, cancellationToken);

        await writer.WriteAsync(new AuditEventDraft
        {
            Module = "Audit",
            EntityType = "audit_events",
            EntityId = "list",
            Action = "VIEW",
            Outcome = AuditOutcome.Success,
            Remarks = $"Viewed page {query.Page} (size {query.PageSize}). Filters: {DescribeFilters(query)}",
        }, cancellationToken);

        return result;
    }

    private static void Validate(AuditQuery query)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (query.Page < 1)
        {
            errors["page"] = ["Page must be 1 or greater."];
        }

        if (query.PageSize < 1 || query.PageSize > MaxPageSize)
        {
            errors["pageSize"] = [$"Page size must be between 1 and {MaxPageSize}."];
        }

        if (query.From is not null && query.To is not null && query.From > query.To)
        {
            errors["to"] = ["The end date must not be earlier than the start date."];
        }

        CheckLength(errors, "module", query.Module, 64);
        CheckLength(errors, "action", query.Action, 100);
        CheckLength(errors, "entityType", query.EntityType, 100);
        CheckLength(errors, "entityId", query.EntityId, 200);

        if (errors.Count > 0)
        {
            throw new EdmException(EdmErrorCodes.ValidationFailed, errors);
        }
    }

    private static void CheckLength(Dictionary<string, string[]> errors, string field, string? value, int max)
    {
        if (value is not null && value.Length > max)
        {
            errors[field] = [$"{field} must be {max} characters or fewer."];
        }
    }

    private static string DescribeFilters(AuditQuery query)
    {
        var parts = new List<string>();
        if (query.From is not null) parts.Add($"from={query.From:O}");
        if (query.To is not null) parts.Add($"to={query.To:O}");
        if (query.Module is not null) parts.Add($"module={query.Module}");
        if (query.Action is not null) parts.Add($"action={query.Action}");
        if (query.EntityType is not null) parts.Add($"entityType={query.EntityType}");
        if (query.EntityId is not null) parts.Add($"entityId={query.EntityId}");
        if (query.ActorUserId is not null) parts.Add($"actorUserId={query.ActorUserId}");
        if (query.Outcome is not null) parts.Add($"outcome={query.Outcome}");
        return parts.Count == 0 ? "none" : string.Join(", ", parts);
    }
}
