using Edm.Identity.Domain;

namespace Edm.Identity.Application;

/// <summary>Persistence port for local accounts. Implemented in Infrastructure. Every query is filtered by tenant.</summary>
public interface IUserAccountStore
{
    Task<UserAccount?> FindByUsernameAsync(Guid tenantId, string username, CancellationToken cancellationToken);

    Task<bool> AnyAsync(Guid tenantId, CancellationToken cancellationToken);

    Task AddAsync(UserAccount user, CancellationToken cancellationToken);

    /// <summary>Writes the failure count and lock time. Nothing else on the account changes here.</summary>
    Task SaveSignInStateAsync(UserAccount user, CancellationToken cancellationToken);
}

/// <summary>Password hashing port. The algorithm and its cost live in Infrastructure.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);

    /// <summary>
    /// Spends the same time as a verification, without a stored hash. Called for unknown and locked accounts,
    /// so a response time does not reveal whether a username exists.
    /// </summary>
    void ConsumeVerificationTime(string password);
}
