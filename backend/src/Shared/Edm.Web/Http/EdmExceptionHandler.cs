using Edm.SharedKernel.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Edm.Web.Http;

/// <summary>Maps <see cref="EdmException"/> to its catalogue code. Other exceptions get the framework's 500 response.</summary>
public sealed class EdmExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not EdmException edm)
        {
            return false;
        }

        await ProblemResponse.WriteAsync(httpContext, edm.Error, edm.FieldErrors);
        return true;
    }
}
