using Edm.SharedKernel.Errors;
using Edm.Web.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace Edm.Web.Authentication;

public static class EdmAuthenticationDefaults
{
    /// <summary>The session scheme: an HttpOnly, SameSite=Strict cookie with an idle timeout (ADR-0006).</summary>
    public const string SessionScheme = "EdmSession";

    public const string CookieName = "edm.session";
}

public static class SessionAuthenticationExtensions
{
    /// <summary>
    /// Adds the session cookie scheme. A request with no valid cookie gets the EDM-AUTH-001 problem (401), and a
    /// signed-in user without the permission gets EDM-AUTH-002 (403). Neither redirects, because this is an API.
    /// </summary>
    public static AuthenticationBuilder AddEdmSession(this AuthenticationBuilder builder, TimeSpan idleTimeout)
    {
        return builder.AddCookie(EdmAuthenticationDefaults.SessionScheme, options =>
        {
            options.Cookie.Name = EdmAuthenticationDefaults.CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.IsEssential = true;
            options.ExpireTimeSpan = idleTimeout;
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
                ProblemResponse.WriteAsync(context.HttpContext, EdmErrorCodes.NotAuthenticated);
            options.Events.OnRedirectToAccessDenied = context =>
                ProblemResponse.WriteAsync(context.HttpContext, EdmErrorCodes.NotPermitted);
        });
    }
}
