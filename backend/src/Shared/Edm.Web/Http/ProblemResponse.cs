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

    /// <summary>Writes the problem to the response. Used by the authentication events.</summary>
    public static Task WriteAsync(
        HttpContext context,
        EdmErrorDefinition error,
        IReadOnlyDictionary<string, string[]>? fieldErrors = null)
    {
        context.Response.StatusCode = error.HttpStatus;
        context.Response.ContentType = ProblemJsonContentType;

        // WriteAsJsonAsync would replace the content type with application/json, so the type is passed explicitly.
        return context.Response.WriteAsJsonAsync(
            Body(context, error, fieldErrors),
            options: null,
            contentType: ProblemJsonContentType,
            cancellationToken: context.RequestAborted);
    }

    /// <summary>The same problem as an endpoint result, for minimal API handlers.</summary>
    public static IResult Result(
        HttpContext context,
        EdmErrorDefinition error,
        IReadOnlyDictionary<string, string[]>? fieldErrors = null)
        => Results.Json(
            data: Body(context, error, fieldErrors),
            contentType: ProblemJsonContentType,
            statusCode: error.HttpStatus);

    private static Dictionary<string, object?> Body(
        HttpContext context,
        EdmErrorDefinition error,
        IReadOnlyDictionary<string, string[]>? fieldErrors)
    {
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

        return body;
    }
}
