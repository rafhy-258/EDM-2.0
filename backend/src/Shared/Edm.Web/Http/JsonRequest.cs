using Microsoft.AspNetCore.Http;

namespace Edm.Web.Http;

public static class JsonRequest
{
    /// <summary>
    /// State-changing requests must send JSON. A cross-site HTML form cannot send application/json without a CORS
    /// preflight, so this blocks form-based cross-site request forgery. Together with SameSite=Strict cookies this
    /// is the CSRF control for ADR-0006. Every state-changing endpoint must check it.
    /// </summary>
    public static bool IsJson(HttpRequest request)
        => request.ContentType is { } type && type.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);
}
