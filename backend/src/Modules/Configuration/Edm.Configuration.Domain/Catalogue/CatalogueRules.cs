namespace Edm.Configuration.Domain.Catalogue;

/// <summary>
/// Validation rules shared by categories and subcategories (Master Prompt Part 5.1, data-model design note section 2.3).
/// Pure functions: no I/O, no framework types. Uniqueness is checked by the application layer and backed by a partial
/// unique index in the database.
/// </summary>
public static class CatalogueRules
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 500;

    /// <summary>Returns the user-facing message for an invalid name, or null when the name is valid.</summary>
    public static string? NameError(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return "Enter a name.";
        }

        return trimmed.Length > MaxNameLength
            ? $"The name must be {MaxNameLength} characters or fewer."
            : null;
    }

    /// <summary>Returns the user-facing message for an invalid description, or null when it is valid.</summary>
    public static string? DescriptionError(string? description)
    {
        return description is not null && description.Trim().Length > MaxDescriptionLength
            ? $"The description must be {MaxDescriptionLength} characters or fewer."
            : null;
    }

    /// <summary>The stored form of a name: trimmed. Call only after NameError returned null.</summary>
    public static string NormaliseName(string name) => name.Trim();

    /// <summary>The stored form of a description: trimmed, and null when empty.</summary>
    public static string? NormaliseDescription(string? description)
    {
        var trimmed = description?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
