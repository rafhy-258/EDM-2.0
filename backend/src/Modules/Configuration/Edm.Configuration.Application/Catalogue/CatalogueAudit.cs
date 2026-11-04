using Edm.Audit.Application;

namespace Edm.Configuration.Application.Catalogue;

/// <summary>Audit helpers shared by the configuration services.</summary>
internal static class CatalogueAudit
{
    public const string Module = "Configuration";

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
