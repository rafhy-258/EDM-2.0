namespace Edm.Audit.Domain;

/// <summary>Whether the audited action succeeded (Part 10.1, outcome).</summary>
public enum AuditOutcome
{
    Success = 0,
    Failure = 1,
}
