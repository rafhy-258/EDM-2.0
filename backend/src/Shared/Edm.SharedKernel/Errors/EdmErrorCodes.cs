namespace Edm.SharedKernel.Errors;

/// <summary>A catalogue entry from Master Prompt Part 21.3: the code, the user-safe message and the HTTP status.</summary>
public sealed record EdmErrorDefinition(string Code, string Message, int HttpStatus);

/// <summary>
/// The starter error-code catalogue (Part 21.3). Messages are the user-facing text from the specification.
/// HTTP statuses are a design choice recorded in the Phase 0 design note (ADR-0007).
/// </summary>
public static class EdmErrorCodes
{
    public static readonly EdmErrorDefinition ValidationFailed = new("EDM-VAL-001", "Some fields need attention. Please check the highlighted fields.", 422);
    public static readonly EdmErrorDefinition NoRouteForCategory = new("EDM-ROUTE-001", "No approval level is configured for this category. Please contact Admin or HR.", 422);
    public static readonly EdmErrorDefinition NoRouteForAmount = new("EDM-ROUTE-002", "No approval level is configured for this amount. Please contact Admin or HR.", 422);
    public static readonly EdmErrorDefinition DepartmentHeadNotFound = new("EDM-ROUTE-003", "Your department head could not be found. Please contact HR.", 422);
    public static readonly EdmErrorDefinition CurrencyNoActiveRate = new("EDM-CUR-001", "This currency has no active exchange rate.", 422);
    public static readonly EdmErrorDefinition DuplicateActiveRule = new("EDM-CFG-001", "An active rule already exists for this combination.", 409);
    public static readonly EdmErrorDefinition EcLevelOverlap = new("EDM-CFG-002", "This amount range overlaps another level.", 409);
    public static readonly EdmErrorDefinition RecordInUse = new("EDM-CFG-003", "This record is used by other records and cannot be deleted.", 409);
    public static readonly EdmErrorDefinition SingleHolderAlreadyActive = new("EDM-CFG-004", "Only one active holder is allowed for this designation.", 409);
    public static readonly EdmErrorDefinition NotCurrentAssignee = new("EDM-WF-001", "You do not have a pending approval on this request.", 403);
    public static readonly EdmErrorDefinition AlreadyActed = new("EDM-WF-002", "You have already acted on this request.", 409);
    public static readonly EdmErrorDefinition ActionNotPermitted = new("EDM-WF-003", "This action is not available to you.", 403);
    public static readonly EdmErrorDefinition RemarksRequired = new("EDM-WF-004", "Please enter remarks.", 422);
    public static readonly EdmErrorDefinition ForwardRecipientRequired = new("EDM-WF-005", "Please select a recipient.", 422);
    public static readonly EdmErrorDefinition QuorumAlreadyMet = new("EDM-WF-006", "The committee quorum has already been met.", 409);
    public static readonly EdmErrorDefinition NotAuthenticated = new("EDM-AUTH-001", "Please sign in.", 401);
    public static readonly EdmErrorDefinition NotPermitted = new("EDM-AUTH-002", "You do not have access to this.", 403);
    public static readonly EdmErrorDefinition StaleUpdate = new("EDM-CONC-001", "This record was changed by someone else. Please reload.", 412);
    public static readonly EdmErrorDefinition FileRejected = new("EDM-FILE-001", "The file could not be accepted.", 422);

    /// <summary>All starter codes, in the order of Part 21.3.</summary>
    public static IReadOnlyList<EdmErrorDefinition> All { get; } =
    [
        ValidationFailed,
        NoRouteForCategory,
        NoRouteForAmount,
        DepartmentHeadNotFound,
        CurrencyNoActiveRate,
        DuplicateActiveRule,
        EcLevelOverlap,
        RecordInUse,
        SingleHolderAlreadyActive,
        NotCurrentAssignee,
        AlreadyActed,
        ActionNotPermitted,
        RemarksRequired,
        ForwardRecipientRequired,
        QuorumAlreadyMet,
        NotAuthenticated,
        NotPermitted,
        StaleUpdate,
        FileRejected,
    ];
}
