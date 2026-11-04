namespace Edm.Identity.Domain;

/// <summary>How many failed sign-ins lock an account, and for how long. Values come from configuration.</summary>
public sealed class LockoutPolicy
{
    public LockoutPolicy(int threshold, TimeSpan duration)
    {
        if (threshold < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), "The lockout threshold must be at least 1.");
        }

        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "The lockout duration must be positive.");
        }

        Threshold = threshold;
        Duration = duration;
    }

    /// <summary>Failed sign-ins in a row that lock the account.</summary>
    public int Threshold { get; }

    /// <summary>How long the account stays locked.</summary>
    public TimeSpan Duration { get; }
}

/// <summary>Password rules. The minimum length is configurable. The other rules are fixed.</summary>
public sealed class PasswordPolicy(int minimumLength)
{
    public const int MaximumLength = 256;

    public int MinimumLength { get; } = minimumLength >= 8
        ? minimumLength
        : throw new ArgumentOutOfRangeException(nameof(minimumLength), "The minimum password length must be at least 8.");

    /// <summary>Returns the problems with a proposed password. An empty list means the password is acceptable.</summary>
    public IReadOnlyList<string> Validate(string username, string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(password))
        {
            problems.Add("The password must not be blank.");
            return problems;
        }

        if (password.Length < MinimumLength)
        {
            problems.Add($"The password must be at least {MinimumLength} characters.");
        }

        if (password.Length > MaximumLength)
        {
            problems.Add($"The password must be at most {MaximumLength} characters.");
        }

        if (username.Length > 0 && password.Contains(username, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("The password must not contain the username.");
        }

        return problems;
    }
}

/// <summary>Username rules. Usernames are stored in lower case.</summary>
public static class UsernameRules
{
    public const int MinimumLength = 3;

    public const int MaximumLength = 64;

    public static string Normalise(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        return raw.Trim().ToLowerInvariant();
    }

    public static bool IsValid(string normalised)
    {
        ArgumentNullException.ThrowIfNull(normalised);
        return normalised.Length is >= MinimumLength and <= MaximumLength
            && normalised.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
    }
}
