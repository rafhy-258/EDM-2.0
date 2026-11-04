using System.Security.Claims;
using System.Text.Encodings.Web;
using Edm.Web.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Edm.IntegrationTests.Support;

/// <summary>
/// Runs the real host against a test database. Authentication is replaced by a header-driven test scheme:
///   X-Test-User: a GUID for the signed-in user (omit for anonymous)
///   X-Test-Permissions: comma-separated permissions
///   X-Test-Department: optional department name
/// </summary>
public sealed class EdmApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Edm", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");

        builder.ConfigureTestServices(services =>
        {
            // Only authenticate uses the test scheme. Challenge and forbid stay on the production deny-by-default
            // scheme, so the 401 and 403 responses are the real EDM-AUTH-001 and EDM-AUTH-002 problem responses.
            services.AddAuthentication(options => options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "TestScheme";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var userId) || string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new("name", "Test User"),
        };

        if (Request.Headers.TryGetValue("X-Test-Department", out var department))
        {
            claims.Add(new Claim(EdmClaimTypes.Department, department.ToString()));
        }

        if (Request.Headers.TryGetValue("X-Test-Permissions", out var permissions))
        {
            foreach (var permission in permissions.ToString()
                         .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new Claim(EdmClaimTypes.Permission, permission));
            }
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
