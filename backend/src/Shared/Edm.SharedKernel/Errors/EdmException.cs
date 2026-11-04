using System.Collections.ObjectModel;

namespace Edm.SharedKernel.Errors;

/// <summary>
/// A business or validation failure carrying a catalogue code. Exception handlers turn it into an
/// RFC 9457 ProblemDetails response with the code, a user-safe message and optional field errors.
/// </summary>
public sealed class EdmException : Exception
{
    private static readonly IReadOnlyDictionary<string, string[]> NoFieldErrors =
        new ReadOnlyDictionary<string, string[]>(new Dictionary<string, string[]>(StringComparer.Ordinal));

    public EdmException(EdmErrorDefinition error, IReadOnlyDictionary<string, string[]>? fieldErrors = null)
        : base(error.Message)
    {
        Error = error;
        FieldErrors = fieldErrors ?? NoFieldErrors;
    }

    public EdmErrorDefinition Error { get; }

    public IReadOnlyDictionary<string, string[]> FieldErrors { get; }
}
