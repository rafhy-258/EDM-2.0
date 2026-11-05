using System.Text.RegularExpressions;

namespace Edm.Configuration.Domain.Authorities;

public static class ApprovalDesignationCodes
{
    public const string EcMember = "EC_MEMBER";
    public const string Ceo = "CEO";
    public const string Cfo = "CFO";
    public const string HeadAdminGst = "HEAD_ADMIN_GST";
    public const string RecommendingAuthority = "RECOMMENDING_AUTHORITY";
    public const string DepartmentHead = "DEPARTMENT_HEAD";

    public static readonly IReadOnlyList<string> MemberDesignations =
        [EcMember, Ceo, Cfo, HeadAdminGst];

    public static readonly IReadOnlyList<string> ActionTagDesignations =
        [DepartmentHead, RecommendingAuthority, EcMember, Ceo, Cfo, HeadAdminGst];

    public static bool IsMemberDesignation(string? value)
        => value is not null && MemberDesignations.Contains(value, StringComparer.Ordinal);

    public static string Label(string code) => code switch
    {
        EcMember => "EC Member",
        Ceo => "CEO",
        Cfo => "CFO",
        HeadAdminGst => "Head of Admin & GST",
        RecommendingAuthority => "Recommending Authority",
        DepartmentHead => "Department Head",
        _ => code,
    };
}

public static class ApprovalActionCodes
{
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
    public const string Revert = "REVERT";
    public const string Forward = "FORWARD";

    public static readonly IReadOnlyList<string> All = [Approve, Reject, Revert, Forward];
    public static readonly IReadOnlyList<string> DefaultDecisionActions = [Approve, Reject, Revert];
}

public sealed record EcMember(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    string Designation,
    bool IsActive,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy);

public sealed record ActionTagSet(
    Guid Id,
    Guid TenantId,
    string Name,
    string Designation,
    IReadOnlyList<string> AllowedActions,
    bool IsActive,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy);

public sealed record RecommendingAuthority(
    Guid Id,
    Guid TenantId,
    string Title,
    Guid DepartmentId,
    Guid UserId,
    Guid DefaultActionTagSetId,
    bool IsActive,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy);

public sealed record AuthorityDirectoryUser(
    Guid Id,
    string EmployeeId,
    string Name,
    Guid DepartmentId,
    string DepartmentName,
    bool IsActive,
    bool IsDepartmentActive);

public static partial class ApprovalAuthorityRules
{
    public const int TitleMaxLength = 200;
    private static readonly HashSet<string> Designations =
        new(ApprovalDesignationCodes.ActionTagDesignations, StringComparer.Ordinal);
    private static readonly HashSet<string> Actions = new(ApprovalActionCodes.All, StringComparer.Ordinal);

    public static string? DesignationError(string? designation)
        => ApprovalDesignationCodes.IsMemberDesignation(designation) ? null : "Choose a valid designation.";

    public static string? TitleError(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Enter a title.";
        }

        var trimmed = title.Trim();
        if (trimmed.Length > TitleMaxLength)
        {
            return "The title must be 200 characters or fewer.";
        }

        return TitlePattern().IsMatch(trimmed) ? null : "Use letters, numbers and spaces only.";
    }

    public static string NormaliseTitle(string title)
        => string.Join(' ', title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string? ActionSetError(string? designation, IReadOnlyCollection<string>? allowedActions)
    {
        if (designation is null || !Designations.Contains(designation))
        {
            return "Choose a valid designation.";
        }

        if (allowedActions is null || allowedActions.Count == 0)
        {
            return "Select at least the Approve action.";
        }

        if (allowedActions.Any(action => !Actions.Contains(action)))
        {
            return "Choose only supported actions.";
        }

        if (allowedActions.Distinct(StringComparer.Ordinal).Count() != allowedActions.Count)
        {
            return "An action can only be selected once.";
        }

        if (!allowedActions.Contains(ApprovalActionCodes.Approve, StringComparer.Ordinal))
        {
            return "Approve must remain available.";
        }

        if (designation == ApprovalDesignationCodes.EcMember &&
            allowedActions.Contains(ApprovalActionCodes.Forward, StringComparer.Ordinal))
        {
            return "EC members cannot be assigned the Forward action.";
        }

        if (designation != ApprovalDesignationCodes.DepartmentHead &&
            allowedActions.Contains(ApprovalActionCodes.Forward, StringComparer.Ordinal))
        {
            return "Forward is available only to the Department Head in Phase 1.";
        }

        return null;
    }

    [GeneratedRegex("^[A-Za-z0-9 ]+$")]
    private static partial Regex TitlePattern();
}
