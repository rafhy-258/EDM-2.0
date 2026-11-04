namespace Edm.SharedKernel.Permissions;

/// <summary>
/// The stored roles of Master Prompt Part 20.2, expanded to explicit permission strings (docs/permission-matrix.md,
/// section 2). Department Head is not stored: it comes from the hierarchy (Part 20.2).
/// The System Administrator set includes four permissions approved on 04-Nov-2026 (open question 1A-8), marked below.
/// </summary>
public static class RoleCatalogue
{
    public const string Requester = "REQUESTER";
    public const string EdmAdministrator = "EDM_ADMINISTRATOR";
    public const string ConfigurationApprover = "CONFIGURATION_APPROVER";
    public const string Auditor = "AUDITOR";
    public const string FinanceViewer = "FINANCE_VIEWER";
    public const string SystemAdministrator = "SYSTEM_ADMINISTRATOR";

    private static readonly IReadOnlyDictionary<string, string[]> Roles = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [Requester] =
        [
            "opex.request.create",
            "opex.request.view.own",
            "opex.request.edit.returned",
            "capex.request.create",
            "capex.request.view.own",
            "capex.request.edit.returned",
            "approval.inbox.view",
            "report.view.own",
        ],
        // All config.* except config.matrix.approve (Part 20.2).
        [EdmAdministrator] = PermissionCatalogue.All
            .Where(p => p.StartsWith("config.", StringComparison.Ordinal) && p != "config.matrix.approve")
            .ToArray(),
        [ConfigurationApprover] =
        [
            "config.matrix.view",
            "config.matrix.approve",
            "config.policy.view",
        ],
        [Auditor] =
        [
            "opex.request.view.all",
            "capex.request.view.all",
            "report.view.all",
            "report.export",
            "audit.view",
            "audit.export",
            "audit.security.view",
            "config.policy.view",
        ],
        [FinanceViewer] =
        [
            "opex.request.view.all",
            "capex.request.view.all",
            "report.view.all",
            "report.export",
        ],
        [SystemAdministrator] =
        [
            // Approved 04-Nov-2026 (open question 1A-8). Part 20.2 grants no role these four. Without them the department,
            // branch, hierarchy and user screens are unreachable.
            "org.department.manage",
            "org.branch.manage",
            "org.hierarchy.manage",
            "org.user.view",
            "org.user.manage",
            "org.sync.run",
            "iam.role.manage",
            "iam.permission.assign",
            "iam.sso.manage",
            "iam.mfa.manage",
            "settings.manage",
            "notification.template.manage",
            "retention.manage",
            "health.view",
            "integration.manage",
        ],
    };

    /// <summary>The roles that are stored against a user account.</summary>
    public static IReadOnlyList<string> StoredRoles { get; } =
    [
        Requester,
        EdmAdministrator,
        ConfigurationApprover,
        Auditor,
        FinanceViewer,
        SystemAdministrator,
    ];

    public static bool IsStoredRole(string role) => Roles.ContainsKey(role);

    public static IReadOnlyList<string> PermissionsFor(string role)
    {
        if (!Roles.TryGetValue(role, out var permissions))
        {
            throw new ArgumentException($"'{role}' is not a stored role.", nameof(role));
        }

        return permissions;
    }
}
