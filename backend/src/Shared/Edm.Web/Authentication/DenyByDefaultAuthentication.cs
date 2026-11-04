using System.Text.Encodings.Web;
using Edm.SharedKernel.Errors;
using Edm.Web.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Edm.Web.Authentication;

public static class EdmAuthenticationDefaults
{
    /// <summary>
    /// The default scheme until SSO is configured (ADR-0006). It never signs anyone in. Every protected
    /// endpoint answers 401 with EDM-AUTH-001, or 403 with EDM-AUTH-002 for a signed-in user without the permission.
    /// </summary>
    public const string Scheme = "EdmDenyByDefault";
}

public sealed class DenyByDefaultAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult(AuthenticateResult.NoResult());

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        => ProblemResponse.WriteAsync(Context, EdmErrorCodes.NotAuthenticated);

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        => ProblemResponse.WriteAsync(Context, EdmErrorCodes.NotPermitted);
}
