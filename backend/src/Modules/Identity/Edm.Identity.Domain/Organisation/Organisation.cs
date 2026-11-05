using System.Text.RegularExpressions;

namespace Edm.Identity.Domain.Organisation;

/// <summary>
/// A department (Master Prompt Part 14.1, data model section 6). The name is unique among active departments of a tenant.
/// A department is deactivated, not deleted, so that history still names it.
/// </summary>
public sealed record Department(
    Guid Id,
    Guid TenantId,
    string Name,
    bool IsActive,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy);

/// <summary>
/// A person in the organisation directory (data model section 6). Each person belongs to one department. The employee ID is
/// unique among all rows of a tenant, so a deactivated person is reactivated, not added again.
/// </summary>
public sealed record DirectoryUser(
    Guid Id,
    Guid TenantId,
    string EmployeeId,
    string Name,
    string? Email,
    Guid DepartmentId,
    bool IsActive,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy);

/// <summary>Field rules for departments and directory users. The server applies them; the web repeats them for quick feedback.</summary>
public static partial class OrganisationRules
{
    public const int NameMaxLength = 200;
    public const int EmployeeIdMaxLength = 32;
    public const int EmailMaxLength = 254;

    public static string? DepartmentNameError(string? name)
        => RequiredText(name, NameMaxLength, "Enter a department name.", "The department name must be 200 characters or fewer.");

    public static string? NameError(string? name)
        => RequiredText(name, NameMaxLength, "Enter a name.", "The name must be 200 characters or fewer.");

    public static string? EmployeeIdError(string? employeeId)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
        {
            return "Enter an employee ID.";
        }

        var trimmed = employeeId.Trim();
        if (trimmed.Length > EmployeeIdMaxLength)
        {
            return "The employee ID must be 32 characters or fewer.";
        }

        return EmployeeIdPattern().IsMatch(trimmed) ? null : "Use letters and numbers only, for example EMP1024.";
    }

    public static string? EmailError(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var trimmed = email.Trim();
        if (trimmed.Length > EmailMaxLength)
        {
            return "The email must be 254 characters or fewer.";
        }

        return EmailPattern().IsMatch(trimmed) ? null : "Enter a valid email address.";
    }

    public static string NormaliseText(string value) => value.Trim();

    public static string NormaliseEmployeeId(string value) => value.Trim().ToUpperInvariant();

    public static string? NormaliseEmail(string? email) => string.IsNullOrWhiteSpace(email) ? null : email.Trim();

    private static string? RequiredText(string? value, int maxLength, string missing, string tooLong)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return missing;
        }

        return value.Trim().Length > maxLength ? tooLong : null;
    }

    [GeneratedRegex("^[A-Za-z0-9]+$")]
    private static partial Regex EmployeeIdPattern();

    [GeneratedRegex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$")]
    private static partial Regex EmailPattern();
}
