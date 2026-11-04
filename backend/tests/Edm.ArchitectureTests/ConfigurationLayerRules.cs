using Edm.Configuration.Api;
using Edm.Configuration.Application;
using Edm.Configuration.Application.Catalogue;
using Edm.Configuration.Domain.Catalogue;
using NetArchTest.Rules;

namespace Edm.ArchitectureTests;

/// <summary>The layer rules of ADR-0001, applied to the configuration module.</summary>
public sealed class ConfigurationLayerRules
{
    private const string Host = "Edm.Host";
    private const string EfCore = "Microsoft.EntityFrameworkCore";
    private const string Npgsql = "Npgsql";
    private const string AspNetCore = "Microsoft.AspNetCore";

    [Fact]
    public void Configuration_domain_depends_on_no_outer_layer_or_framework()
    {
        var result = Types.InAssembly(typeof(Category).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Edm.Configuration.Application",
                "Edm.Configuration.Infrastructure",
                "Edm.Configuration.Api",
                "Edm.Web",
                Host,
                EfCore,
                Npgsql,
                AspNetCore)
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void Configuration_application_does_not_depend_on_infrastructure_api_or_the_web()
    {
        var result = Types.InAssembly(typeof(CategoryService).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Edm.Configuration.Infrastructure",
                "Edm.Configuration.Api",
                "Edm.Web",
                Host,
                EfCore,
                Npgsql,
                AspNetCore)
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void Configuration_api_does_not_reach_into_infrastructure()
    {
        var result = Types.InAssembly(typeof(ConfigurationEndpoints).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Edm.Configuration.Infrastructure", Host, EfCore, Npgsql)
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void Configuration_does_not_reach_into_identity_or_other_module_tables()
    {
        var result = Types.InAssembly(typeof(ConfigurationApplicationServices).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Edm.Identity.Application", "Edm.Identity.Infrastructure", "Edm.Identity.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    private static string Failures(NetArchTest.Rules.TestResult result)
        => "Architecture rule violated by: " + string.Join(", ", result.FailingTypeNames ?? []);
}
