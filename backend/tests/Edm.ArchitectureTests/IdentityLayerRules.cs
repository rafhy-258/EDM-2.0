using Edm.Identity.Api;
using Edm.Identity.Application;
using Edm.Identity.Domain;
using NetArchTest.Rules;

namespace Edm.ArchitectureTests;

/// <summary>The layer rules of ADR-0001, applied to the identity module.</summary>
public sealed class IdentityLayerRules
{
    private const string Host = "Edm.Host";
    private const string EfCore = "Microsoft.EntityFrameworkCore";
    private const string Npgsql = "Npgsql";
    private const string AspNetCore = "Microsoft.AspNetCore";

    [Fact]
    public void Identity_domain_depends_on_no_outer_layer_or_framework()
    {
        var result = Types.InAssembly(typeof(UserAccount).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Edm.Identity.Application",
                "Edm.Identity.Infrastructure",
                "Edm.Identity.Api",
                "Edm.Web",
                Host,
                EfCore,
                Npgsql,
                AspNetCore)
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void Identity_application_does_not_depend_on_infrastructure_api_or_the_web()
    {
        var result = Types.InAssembly(typeof(SignInService).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Edm.Identity.Infrastructure",
                "Edm.Identity.Api",
                "Edm.Web",
                Host,
                EfCore,
                Npgsql,
                AspNetCore)
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void Identity_api_does_not_reach_into_infrastructure()
    {
        var result = Types.InAssembly(typeof(IdentityEndpoints).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Edm.Identity.Infrastructure", Host, EfCore, Npgsql)
            .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    private static string Failures(NetArchTest.Rules.TestResult result)
        => "Architecture rule violated by: " + string.Join(", ", result.FailingTypeNames ?? []);
}
