namespace Edm.Identity.Application;

/// <summary>
/// Settings for local accounts, bound from the "Identity" configuration section. The defaults are PROPOSED values
/// (open questions P0-1 to P0-3). The Master Prompt does not give numbers for lockout, session timeout or password length.
/// </summary>
public sealed class IdentityOptions
{
    /// <summary>PBKDF2-SHA256 iterations for new hashes.</summary>
    public int PasswordIterations { get; set; } = 600_000;

    public int MinimumPasswordLength { get; set; } = 12;

    /// <summary>Failed sign-ins in a row that lock the account.</summary>
    public int LockoutThreshold { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 15;

    /// <summary>The session ends after this many idle minutes. Each request in the session renews it.</summary>
    public int IdleTimeoutMinutes { get; set; } = 20;

    /// <summary>Creates the first administrator when no account exists. Used once, on a new database.</summary>
    public BootstrapOptions Bootstrap { get; set; } = new();
}

public sealed class BootstrapOptions
{
    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary>Comma-separated stored roles (Part 20.2). The default gives System Administrator and Auditor.</summary>
    public string? Roles { get; set; } = "SYSTEM_ADMINISTRATOR,AUDITOR";

    public string? DisplayName { get; set; } = "Administrator";
}
