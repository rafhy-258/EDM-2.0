using Edm.SharedKernel.Errors;
using Microsoft.AspNetCore.Http;

namespace Edm.Web.Http;

/// <summary>Correlation id access for the HTTP pipeline. The middleware stores the id in HttpContext.Items.</summary>
public static class CorrelationKeys
{
    public const string HeaderName = "X-Correlation-Id";

    public const string HttpItemKey = "edm.correlationId";

    public static string Current(HttpContext context)
        => context.Items[HttpItemKey] as string ?? context.TraceIdentifier;
}

/// <summary>Writes RFC 9457 ProblemDetails with the EDM error code (Part 21.1).</summary>
public static class ProblemResponse
{
    public const string ProblemJsonContentType = "application/problem+json";

    public static Task WriteAsync(
        HttpContext context,
        EdmErrorDefinition error,
        IReadOnlyDictionary<string, string[]>? fieldErrors = null)
    {
        context.Response.StatusCode = error.HttpStatus;
        context.Response.ContentType = ProblemJsonContentType;

        var body = new Dictionary<string, object?>
        {
            ["type"] = $"urn:edm:error:{error.Code}",
            ["title"] = "The request could not be completed",
            ["status"] = error.HttpStatus,
            ["detail"] = error.Message,
            ["code"] = error.Code,
            ["message"] = error.Message,
            ["correlationId"] = CorrelationKeys.Current(context),
        };

        if (fieldErrors is { Count: > 0 })
        {
            body["errors"] = fieldErrors;
        }

        return context.Response.WriteAsJsonAsync(body, context.RequestAborted);
    }
}
