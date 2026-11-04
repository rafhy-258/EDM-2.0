using System.Security.Claims;
using Edm.SharedKernel.Context;
using Edm.Web.Authorization;
using Microsoft.AspNetCore.Http;

namespace Edm.Web.Http;

/// <summary>Reads the correlation id that <c>CorrelationIdMiddleware</c> stored for this request.</summary>
public sealed class HttpCorrelationIdAccessor(IHttpContextAccessor accessor) : ICorrelationIdAccessor
{
    public string CorrelationId
        => accessor.HttpContext is { } context ? CorrelationKeys.Current(context) : "no-request";
}

/// <summary>
/// Builds the acting user from the request principal. Claims: "sub" is the user id, "name" the display
/// name, <see cref="EdmClaimTypes.Department"/> the department and ClaimTypes.Role the roles.
/// </summary>
public sealed class HttpActorContext(IHttpContextAccessor accessor) : IActorContext
{
    private HttpContext? Context => accessor.HttpContext;

    private ClaimsPrincipal? Principal => Context?.User;

    public Guid? UserId
        => Guid.TryParse(Principal?.FindFirst("sub")?.Value, out var id) ? id : null;

    public string DisplayName
    {
        get
        {
            var name = Principal?.FindFirst("name")?.Value;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return Principal?.Identity?.IsAuthenticated == true ? "Signed-in user" : "Anonymous";
        }
    }

    public string? Department => Principal?.FindFirst(EdmClaimTypes.Department)?.Value;

    public IReadOnlyList<string> Roles
    {
        get
        {
            var roles = Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
            return roles ?? Array.Empty<string>();
        }
    }

    public string? SourceIp => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var agent = Context?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(agent) ? null : agent;
        }
    }
}
