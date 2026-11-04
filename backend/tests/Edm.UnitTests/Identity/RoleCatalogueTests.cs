using Edm.Identity.Application;
using Edm.SharedKernel.Permissions;

namespace Edm.UnitTests.Identity;

/// <summary>Checks the role mapping against docs/permission-matrix.md section 2 (Part 20.2).</summary>
public sealed class RoleCatalogueTests
{
    [Fact]
    public void The_stored_roles_are_the_six_roles_in_part_20_2()
    {
        Assert.Equal(
            new[]
            {
                "REQUESTER",
                "EDM_ADMINISTRATOR",
                "CONFIGURATION_APPROVER",
                "AUDITOR",
                "FINANCE_VIEWER",
                "SYSTEM_ADMINISTRATOR",
            },
            RoleCatalogue.StoredRoles.ToArray());
    }

    [Fact]
    public void Every_role_permission_is_in_the_permission_catalogue()
    {
        foreach (var role in RoleCatalogue.StoredRoles)
        {
            foreach (var permission in RoleCatalogue.PermissionsFor(role))
            {
                Assert.True(PermissionCatalogue.IsKnown(permission), $"{role} grants unknown permission {permission}.");
            }
        }
    }

    [Fact]
    public void EDM_administrator_holds_all_configuration_except_matrix_approval()
    {
        var permissions = RoleCatalogue.PermissionsFor(RoleCatalogue.EdmAdministrator);

        Assert.Contains("config.matrix.manage", permissions);
        Assert.Contains("config.limit.manage", permissions);
        Assert.DoesNotContain("config.matrix.approve", permissions);
        Assert.DoesNotContain("org.department.manage", permissions);
        Assert.DoesNotContain("org.user.view", permissions);
    }

    [Fact]
    public void Configuration_approver_can_approve_the_matrix_and_nothing_else_in_configuration()
    {
        var permissions = RoleCatalogue.PermissionsFor(RoleCatalogue.ConfigurationApprover);

        Assert.Contains("config.matrix.approve", permissions);
        Assert.DoesNotContain("config.matrix.manage", permissions);
        Assert.DoesNotContain("config.limit.manage", permissions);
    }

    [Fact]
    public void Auditor_can_view_and_export_the_audit_trail_but_cannot_manage_users()
    {
        var permissions = RoleCatalogue.PermissionsFor(RoleCatalogue.Auditor);

        Assert.Contains("audit.view", permissions);
        Assert.Contains("audit.export", permissions);
        Assert.DoesNotContain("org.user.manage", permissions);
        Assert.DoesNotContain("iam.role.manage", permissions);
    }

    [Fact]
    public void Requester_has_no_audit_organisation_or_identity_permission()
    {
        var permissions = RoleCatalogue.PermissionsFor(RoleCatalogue.Requester);

        Assert.DoesNotContain(permissions, p => p.StartsWith("audit.", StringComparison.Ordinal));
        Assert.DoesNotContain(permissions, p => p.StartsWith("org.", StringComparison.Ordinal));
        Assert.DoesNotContain(permissions, p => p.StartsWith("iam.", StringComparison.Ordinal));
    }

    [Fact]
    public void System_administrator_holds_the_identity_permissions_but_not_approval()
    {
        var permissions = RoleCatalogue.PermissionsFor(RoleCatalogue.SystemAdministrator);

        Assert.Contains("iam.role.manage", permissions);
        Assert.Contains("org.user.manage", permissions);
        Assert.DoesNotContain("approval.act", permissions);
        Assert.DoesNotContain("config.matrix.approve", permissions);
    }

    [Fact]
    public void A_user_gets_the_union_of_the_permissions_of_their_roles()
    {
        var permissions = CurrentSession.PermissionsOf(new[] { RoleCatalogue.Auditor, RoleCatalogue.FinanceViewer });

        Assert.Contains("audit.view", permissions);
        Assert.Contains("report.export", permissions);
        Assert.Equal(
            permissions.Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray(),
            permissions.ToArray());
    }

    [Fact]
    public void Asking_for_the_permissions_of_an_unknown_role_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => RoleCatalogue.PermissionsFor("NOT_A_ROLE"));
    }
}
