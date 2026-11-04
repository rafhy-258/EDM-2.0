using Edm.SharedKernel.Permissions;

namespace Edm.Identity.Domain;

/// <summary>
/// A local account (ADR-0006). Holds the sign-in state that the lockout rule needs. The password is stored only as a hash.
/// </summary>
public sealed class UserAccount
{
    private readonly List<string> _roles;

    private UserAccount(
        Guid id,
        Guid tenantId,
        string username,
        string displayName,
        string? department,
        string passwordHash,
        bool isActive,
        int failedSignIns,
        DateTimeOffset? lockedUntilUtc,
        IEnumerable<string> roles)
    {
        Id = id;
        TenantId = tenantId;
        Username = username;
        DisplayName = displayName;
        Department = department;
        PasswordHash = passwordHash;
        IsActive = isActive;
        FailedSignIns = failedSignIns;
        LockedUntilUtc = lockedUntilUtc;
        _roles = roles.ToList();
    }

    public Guid Id { get; }

    public Guid TenantId { get; }

    /// <summary>Lower case, unique within the tenant.</summary>
    public string Username { get; }

    public string DisplayName { get; }

    public string? Department { get; }

    /// <summary>A PBKDF2 hash in the format written by the infrastructure hasher. Never the password itself.</summary>
    public string PasswordHash { get; }

    public bool IsActive { get; }

    public int FailedSignIns { get; private set; }

    public DateTimeOffset? LockedUntilUtc { get; private set; }

    public IReadOnlyList<string> Roles => _roles;

    /// <summary>Creates a new account. Every role must be a stored role (Part 20.2).</summary>
    public static UserAccount Create(
        Guid id,
        Guid tenantId,
        string username,
        string displayName,
        string? department,
        string passwordHash,
        IEnumerable<string> roles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentNullException.ThrowIfNull(roles);

        var roleList = roles.ToList();
        foreach (var role in roleList)
        {
            if (!RoleCatalogue.IsStoredRole(role))
            {
                throw new ArgumentException($"'{role}' is not a stored role.", nameof(roles));
            }
        }

        return new UserAccount(id, tenantId, username, displayName, department, passwordHash, true, 0, null, roleList);
    }

    /// <summary>Rebuilds an account from storage. The rules were applied when the state was written.</summary>
    public static UserAccount Restore(
        Guid id,
        Guid tenantId,
        string username,
        string displayName,
        string? department,
        string passwordHash,
        bool isActive,
        int failedSignIns,
        DateTimeOffset? lockedUntilUtc,
        IEnumerable<string> roles)
        => new(id, tenantId, username, displayName, department, passwordHash, isActive, failedSignIns, lockedUntilUtc, roles);

    public bool IsLockedAt(DateTimeOffset now) => LockedUntilUtc is { } until && until > now;

    /// <summary>
    /// Counts a failed password. Returns true when this failure locks the account. A lock that has expired
    /// starts a new count.
    /// </summary>
    public bool RecordFailedSignIn(DateTimeOffset now, LockoutPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (LockedUntilUtc is { } until && until <= now)
        {
            LockedUntilUtc = null;
            FailedSignIns = 0;
        }

        FailedSignIns++;
        if (FailedSignIns < policy.Threshold)
        {
            return false;
        }

        LockedUntilUtc = now + policy.Duration;
        return true;
    }

    /// <summary>A successful password clears the failure count and any lock.</summary>
    public void RecordSuccessfulSignIn()
    {
        FailedSignIns = 0;
        LockedUntilUtc = null;
    }
}
