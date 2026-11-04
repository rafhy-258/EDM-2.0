using Edm.Identity.Domain;
using Edm.SharedKernel.Permissions;

namespace Edm.Identity.Application;

/// <summary>What the session knows about the signed-in user: identity, roles and the permissions the roles grant.</summary>
public sealed record CurrentSession(
    Guid UserId,
    string Username,
    string DisplayName,
    string? Department,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions)
{
    public static CurrentSession From(UserAccount user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new CurrentSession(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Department,
            user.Roles,
            PermissionsOf(user.Roles));
    }

    /// <summary>The distinct permissions of the roles, sorted. A permission is granted by any role that holds it.</summary>
    public static IReadOnlyList<string> PermissionsOf(IEnumerable<string> roles)
        => roles
            .SelectMany(role => RoleCatalogue.PermissionsFor(role))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();
}

/// <summary>Result of a sign-in attempt. A rejected attempt never says why to the caller.</summary>
public sealed record SignInResult(bool Succeeded, CurrentSession? Session)
{
    public static SignInResult Rejected() => new(false, null);

    public static SignInResult Accepted(CurrentSession session) => new(true, session);
}
