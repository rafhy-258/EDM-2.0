using Edm.Audit.Api;
using Edm.Audit.Application;
using Edm.Audit.Domain;
using Edm.Audit.Infrastructure;
using Edm.SharedKernel.Context;
using Edm.Web.Authorization;
using NetArchTest.Rules;

namespace Edm.ArchitectureTests;

/// <summary>
/// Dependency rules from ADR-0001: Domain depends on nothing outside itself and the shared kernel,
/// Application does not depend on infrastructure or the web, and the shared kernel is framework-free.
/// </summary>
public sealed class LayerRules
{
    private const string Host = "Edm.Host";
    private const string EfCore = "Microsoft.EntityFrameworkCore";
    private const string Npgsql = "Npgsql";
    private const string AspNetCore = "Microsoft.AspNetCore";

    [Fact]
    public void Audit_domain_depends_on_no_outer_layer_or_framework()
    {
        var result = Types.InAssembly(typeof(AuditEvent).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Edm.Audit.Application",
                "Edm.Audit.Infrastructure",
                "Edm.Audit.Api",
                "Edm.Web",
                Host,
                EfCore,
                Npgsql,
                AspNetCore)
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void Audit_application_does_not_depend_on_infrastructure_api_or_the_web()
    {
        var result = Types.InAssembly(typeof(AuditWriter).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Edm.Audit.Infrastructure",
                "Edm.Audit.Api",
                "Edm.Web",
                Host,
                EfCore,
                Npgsql,
                AspNetCore)
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void Audit_api_does_not_reach_into_infrastructure()
    {
        var result = Types.InAssembly(typeof(AuditEndpoints).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Edm.Audit.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void The_shared_kernel_is_framework_free()
    {
        var result = Types.InAssembly(typeof(IClock).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Edm.Web", "Edm.Audit", Host, EfCore, Npgsql, AspNetCore)
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void The_web_adapters_do_not_depend_on_any_module()
    {
        var result = Types.InAssembly(typeof(RequiresPermissionAttribute).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Edm.Audit", Host, EfCore, Npgsql)
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    private static string Failures(NetArchTest.Rules.TestResult result)
        => "Architecture rule violated by: " + string.Join(", ", result.FailingTypeNames ?? []);
}
