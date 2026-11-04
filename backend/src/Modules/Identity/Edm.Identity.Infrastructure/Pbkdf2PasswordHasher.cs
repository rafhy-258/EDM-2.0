using System.Globalization;
using System.Security.Cryptography;
using Edm.Identity.Application;

namespace Edm.Identity.Infrastructure;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing with a random salt per password. Stored format:
/// <c>PBKDF2-SHA256$iterations$saltBase64$hashBase64</c>. The iteration count is stored with each hash,
/// so raising the setting does not break existing passwords.
/// </summary>
public sealed class Pbkdf2PasswordHasher(IdentityOptions options) : IPasswordHasher
{
    private const string Prefix = "PBKDF2-SHA256";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int MaximumIterations = 10_000_000;
    private static readonly byte[] DummySalt = new byte[SaltSize];

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, options.PasswordIterations, HashAlgorithmName.SHA256, KeySize);
        return string.Join(
            '$',
            Prefix,
            options.PasswordIterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key));
    }

    public bool Verify(string password, string passwordHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(passwordHash);

        var parts = passwordHash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix)
        {
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations)
            || iterations < 1
            || iterations > MaximumIterations)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (expected.Length == 0)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public void ConsumeVerificationTime(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        _ = Rfc2898DeriveBytes.Pbkdf2(password, DummySalt, options.PasswordIterations, HashAlgorithmName.SHA256, KeySize);
    }
}
