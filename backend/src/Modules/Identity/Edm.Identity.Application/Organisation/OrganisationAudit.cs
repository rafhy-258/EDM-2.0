using Edm.Audit.Application;

namespace Edm.Identity.Application.Organisation;

/// <summary>Audit helpers for the organisation directory. Every change carries the old and new values.</summary>
internal static class OrganisationAudit
{
    public const string Module = "Organisation";

    public static AuditEventDraft Draft(string entityType, Guid id, string action, object? before, object? after)
        => new()
        {
            Module = Module,
            EntityType = entityType,
            EntityId = id.ToString(),
            Action = action,
            Before = before,
            After = after,
        };

    /// <summary>"Deactivated" and "Reactivated" when the active flag changes; otherwise "Updated".</summary>
    public static string ActionFor(bool wasActive, bool isActive)
    {
        if (wasActive == isActive)
        {
            return "Updated";
        }

        return isActive ? "Reactivated" : "Deactivated";
    }
}

internal sealed record DepartmentSnapshot(string Name, bool IsActive);

internal sealed record DirectoryUserSnapshot(
    string EmployeeId,
    string Name,
    string? Email,
    Guid DepartmentId,
    bool IsActive);
