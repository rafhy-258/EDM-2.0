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
/// The first administrator the host creates on a new database. Only used when a test asks for it.
/// </summary>
public sealed record BootstrapAccount(string Username, string Password, string Roles);

/// <summary>
/// Runs the real host against a test database.
/// Two modes:
///   - Default: authentication is replaced by a header-driven test scheme:
///       X-Test-User: a GUID for the signed-in user (omit for anonymous)
///       X-Test-Permissions: comma-separated permissions
///       X-Test-Department: optional department name
///   - useTestAuthentication: false: the real EdmSession cookie scheme runs, so sign-in and the cookie are exercised.
/// </summary>
public sealed class EdmApiFactory(
    string connectionString,
    bool useTestAuthentication = true,
    BootstrapAccount? bootstrap = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Edm", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");

        // Low iteration count keeps the tests fast. Production uses the configured default.
        builder.UseSetting("Identity:PasswordIterations", "1000");

        if (bootstrap is not null)
        {
            builder.UseSetting("Identity:Bootstrap:Username", bootstrap.Username);
            builder.UseSetting("Identity:Bootstrap:Password", bootstrap.Password);
            builder.UseSetting("Identity:Bootstrap:Roles", bootstrap.Roles);
        }

        if (useTestAuthentication)
        {
            // Only authenticate uses the test scheme. Challenge and forbid stay on the production session scheme,
            // so 401 and 403 are the real EDM-AUTH-001 and EDM-AUTH-002 problem responses.
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options => options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            });
        }
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
