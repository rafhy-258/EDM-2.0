using System.Security.Claims;
using System.Text.Json;
using Edm.Audit.Application;
using Edm.Audit.Domain;
using Edm.Identity.Application;
using Edm.SharedKernel.Errors;
using Edm.Web.Authentication;
using Edm.Web.Authorization;
using Edm.Web.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Edm.Identity.Api;

public sealed record SignInRequest(string? Username, string? Password);

/// <summary>The signed-in user, as the client needs it to build the menu.</summary>
public sealed record SessionDto(
    Guid UserId,
    string Username,
    string DisplayName,
    string? Department,
    string[] Roles,
    string[] Permissions)
{
    public static SessionDto From(CurrentSession session) => new(
        session.UserId,
        session.Username,
        session.DisplayName,
        session.Department,
        session.Roles.ToArray(),
        session.Permissions.ToArray());

    /// <summary>Reads the session back from the cookie principal. Returns null when the principal has no user id.</summary>
    public static SessionDto? FromPrincipal(ClaimsPrincipal principal)
    {
        if (!Guid.TryParse(principal.FindFirst("sub")?.Value, out var userId))
        {
            return null;
        }

        return new SessionDto(
            userId,
            principal.FindFirst(SessionPrincipal.UsernameClaim)?.Value ?? string.Empty,
            principal.FindFirst("name")?.Value ?? string.Empty,
            principal.FindFirst(EdmClaimTypes.Department)?.Value,
            principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray(),
            principal.FindAll(EdmClaimTypes.Permission)
                .Select(c => c.Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray());
    }
}

/// <summary>Builds the cookie principal from a session. The permissions are claims, so the permission policy works unchanged.</summary>
public static class SessionPrincipal
{
    public const string UsernameClaim = "username";

    public static ClaimsPrincipal Create(CurrentSession session)
    {
        var claims = new List<Claim>
        {
            new("sub", session.UserId.ToString()),
            new("name", session.DisplayName),
            new(UsernameClaim, session.Username),
        };

        if (!string.IsNullOrEmpty(session.Department))
        {
            claims.Add(new Claim(EdmClaimTypes.Department, session.Department));
        }

        claims.AddRange(session.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(session.Permissions.Select(permission => new Claim(EdmClaimTypes.Permission, permission)));

        var identity = new ClaimsIdentity(claims, EdmAuthenticationDefaults.SessionScheme, "name", ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }
}

/// <summary>
/// Local sign-in endpoints (ADR-0006). These three are the only anonymous endpoints besides the health probes:
/// a user cannot have a permission before signing in. Each one is named in the Phase 0 design note for owner approval.
/// </summary>
public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Identity");

        // The body is read here, not bound by the framework, so the JSON check runs first and a form post gets 415.
        group.MapPost("/sign-in", async (HttpContext http, SignInService signIn, CancellationToken cancellationToken) =>
            {
                if (!JsonRequest.IsJson(http.Request))
                {
                    return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
                }

                SignInRequest? body;
                try
                {
                    body = await JsonSerializer.DeserializeAsync<SignInRequest>(
                        http.Request.Body, JsonSerializerOptions.Web, cancellationToken);
                }
                catch (JsonException)
                {
                    body = null;
                }

                var username = body?.Username;
                var password = body?.Password;
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
                {
                    var fieldErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);
                    if (string.IsNullOrWhiteSpace(username))
                    {
                        fieldErrors["username"] = ["Enter your username."];
                    }

                    if (string.IsNullOrEmpty(password))
                    {
                        fieldErrors["password"] = ["Enter your password."];
                    }

                    throw new EdmException(EdmErrorCodes.ValidationFailed, fieldErrors);
                }

                var result = await signIn.SignInAsync(username, password, cancellationToken);
                if (!result.Succeeded || result.Session is null)
                {
                    return ProblemResponse.Result(http, EdmErrorCodes.NotAuthenticated);
                }

                await http.SignInAsync(
                    EdmAuthenticationDefaults.SessionScheme,
                    SessionPrincipal.Create(result.Session),
                    new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });

                return Results.Ok(SessionDto.From(result.Session));
            })
            .AllowAnonymous()
            .WithName("SignIn")
            .WithSummary("Sign in with a local account. Sets the session cookie.");

        group.MapPost("/sign-out", async (HttpContext http, IAuditWriter audit, CancellationToken cancellationToken) =>
            {
                if (!JsonRequest.IsJson(http.Request))
                {
                    return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
                }

                var session = await http.AuthenticateAsync(EdmAuthenticationDefaults.SessionScheme);
                if (session.Succeeded && session.Principal is not null)
                {
                    var userId = session.Principal.FindFirst("sub")?.Value ?? "unknown";
                    await audit.WriteAsync(
                        new AuditEventDraft
                        {
                            Module = "Identity",
                            EntityType = "User",
                            EntityId = userId,
                            Action = "SignOut",
                        },
                        cancellationToken);
                }

                await http.SignOutAsync(EdmAuthenticationDefaults.SessionScheme);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithName("SignOut")
            .WithSummary("End the current session.");

        group.MapGet("/session", async (HttpContext http) =>
            {
                var result = await http.AuthenticateAsync(EdmAuthenticationDefaults.SessionScheme);
                var session = result.Succeeded && result.Principal is not null
                    ? SessionDto.FromPrincipal(result.Principal)
                    : null;

                return session is null
                    ? ProblemResponse.Result(http, EdmErrorCodes.NotAuthenticated)
                    : Results.Ok(session);
            })
            .AllowAnonymous()
            .WithName("GetSession")
            .WithSummary("The signed-in user and their permissions, or 401 when there is no session.");

        return app;
    }
}
