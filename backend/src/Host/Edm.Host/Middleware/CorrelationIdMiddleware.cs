using System.Text.RegularExpressions;
using Edm.Web.Http;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Edm.Host.Middleware;

/// <summary>
/// Reads X-Correlation-Id from the request, or creates one. Stores it for audit and error responses,
/// returns it in the response header, and adds it to every log line of the request.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    [GeneratedRegex("^[A-Za-z0-9._-]{1,128}$")]
    private static partial Regex ValidIdPattern();

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationKeys.HeaderName].ToString();
        var id = ValidIdPattern().IsMatch(incoming) ? incoming : Guid.NewGuid().ToString("N");

        context.Items[CorrelationKeys.HttpItemKey] = id;
        context.Response.Headers[CorrelationKeys.HeaderName] = id;

        using (LogContext.PushProperty("CorrelationId", id))
        {
            await next(context);
        }
    }
}
