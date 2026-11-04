namespace Edm.SharedKernel.Permissions;

/// <summary>
/// The permission catalogue from Master Prompt Part 20.1. Permissions are strings checked on the server.
/// An integration test keeps this list equal to docs/permission-matrix.md, so the two cannot drift apart.
/// </summary>
public static class PermissionCatalogue
{
    private static readonly string[] Permissions =
    [
        // Requests
        "opex.request.create",
        "opex.request.view.own",
        "opex.request.view.department",
        "opex.request.view.all",
        "opex.request.edit.returned",
        "capex.request.create",
        "capex.request.view.own",
        "capex.request.view.department",
        "capex.request.view.all",
        "capex.request.edit.returned",
        // Approvals
        "approval.inbox.view",
        "approval.act",
        "approval.view.assigned",
        // Configuration
        "config.category.view",
        "config.category.manage",
        "config.subcategory.manage",
        "config.matrix.view",
        "config.matrix.manage",
        "config.matrix.approve",
        "config.ec.pool.manage",
        "config.ec.level.manage",
        "config.ra.manage",
        "config.limit.manage",
        "config.currency.manage",
        "config.capex.eligibility.manage",
        "config.actiontags.manage",
        "config.policy.view",
        // Organisation
        "org.department.manage",
        "org.branch.manage",
        "org.hierarchy.manage",
        "org.user.view",
        "org.user.manage",
        "org.sync.run",
        // Identity and access
        "iam.role.manage",
        "iam.permission.assign",
        "iam.sso.manage",
        "iam.mfa.manage",
        // Reports
        "report.view.own",
        "report.view.department",
        "report.view.all",
        "report.export",
        // Audit
        "audit.view",
        "audit.export",
        "audit.security.view",
        // System
        "settings.manage",
        "notification.template.manage",
        "retention.manage",
        "health.view",
        "integration.manage",
    ];

    private static readonly HashSet<string> Known = new(Permissions, StringComparer.Ordinal);

    /// <summary>All permission strings, in catalogue order.</summary>
    public static IReadOnlyList<string> All => Permissions;

    /// <summary>True only for a string in the Part 20.1 catalogue.</summary>
    public static bool IsKnown(string permission) => Known.Contains(permission);
}
