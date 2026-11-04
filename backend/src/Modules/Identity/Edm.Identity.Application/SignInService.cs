using Edm.Audit.Application;
using Edm.Audit.Domain;
using Edm.Identity.Domain;
using Edm.SharedKernel.Context;

namespace Edm.Identity.Application;

/// <summary>
/// Sign-in with a local account (ADR-0006). Rules: a locked or disabled account is never password-checked, a failed
/// password counts towards the lockout, and every attempt writes an audit event (Part 10.2). The caller gets the
/// same answer for an unknown user, a wrong password, a disabled account and a locked account.
/// </summary>
public sealed class SignInService(
    IUserAccountStore store,
    IPasswordHasher hasher,
    IAuditWriter audit,
    ITenantContext tenant,
    IClock clock,
    LockoutPolicy lockout)
{
    private const string Module = "Identity";
    private const string EntityType = "User";

    public async Task<SignInResult> SignInAsync(string username, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        var name = UsernameRules.Normalise(username);
        var now = clock.UtcNow;
        var user = await store.FindByUsernameAsync(tenant.TenantId, name, cancellationToken);

        if (user is null)
        {
            hasher.ConsumeVerificationTime(password);
            await WriteAsync(name, "SignInFailed", AuditOutcome.Failure, "Unknown username", cancellationToken);
            return SignInResult.Rejected();
        }

        if (!user.IsActive)
        {
            hasher.ConsumeVerificationTime(password);
            await WriteAsync(user.Id.ToString(), "SignInFailed", AuditOutcome.Failure, "Account is disabled", cancellationToken);
            return SignInResult.Rejected();
        }

        if (user.IsLockedAt(now))
        {
            hasher.ConsumeVerificationTime(password);
            await WriteAsync(user.Id.ToString(), "SignInFailed", AuditOutcome.Failure, "Account is locked", cancellationToken);
            return SignInResult.Rejected();
        }

        if (!hasher.Verify(password, user.PasswordHash))
        {
            var lockedNow = user.RecordFailedSignIn(now, lockout);
            await store.SaveSignInStateAsync(user, cancellationToken);
            await WriteAsync(user.Id.ToString(), "SignInFailed", AuditOutcome.Failure, "Invalid password", cancellationToken);
            if (lockedNow)
            {
                await WriteAsync(user.Id.ToString(), "AccountLocked", AuditOutcome.Failure, "Too many failed sign-ins", cancellationToken);
            }

            return SignInResult.Rejected();
        }

        user.RecordSuccessfulSignIn();
        await store.SaveSignInStateAsync(user, cancellationToken);
        await WriteAsync(user.Id.ToString(), "SignIn", AuditOutcome.Success, null, cancellationToken);
        return SignInResult.Accepted(CurrentSession.From(user));
    }

    private Task WriteAsync(string entityId, string action, AuditOutcome outcome, string? failureReason, CancellationToken cancellationToken)
        => audit.WriteAsync(
            new AuditEventDraft
            {
                Module = Module,
                EntityType = EntityType,
                EntityId = entityId,
                Action = action,
                Outcome = outcome,
                FailureReason = failureReason,
            },
            cancellationToken);
}

/// <summary>
/// Creates the first administrator when the tenant has no accounts. Runs at startup, only when the bootstrap
/// username is configured. The password comes from configuration or the environment, never from source.
/// </summary>
public sealed class BootstrapAdministrator(
    IUserAccountStore store,
    IPasswordHasher hasher,
    IAuditWriter audit,
    ITenantContext tenant,
    PasswordPolicy passwordPolicy)
{
    /// <summary>Returns true when an account was created.</summary>
    public async Task<bool> EnsureAsync(BootstrapOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Username))
        {
            return false;
        }

        if (await store.AnyAsync(tenant.TenantId, cancellationToken))
        {
            return false;
        }

        var username = UsernameRules.Normalise(options.Username);
        if (!UsernameRules.IsValid(username))
        {
            throw new InvalidOperationException(
                $"The bootstrap username must be {UsernameRules.MinimumLength} to {UsernameRules.MaximumLength} characters: letters, digits, dot, underscore or hyphen.");
        }

        var password = options.Password ?? string.Empty;
        var problems = passwordPolicy.Validate(username, password);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException("The bootstrap password is not acceptable. " + string.Join(' ', problems));
        }

        var roles = (options.Roles ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (roles.Length == 0)
        {
            throw new InvalidOperationException("The bootstrap account needs at least one role.");
        }

        foreach (var role in roles)
        {
            if (!Edm.SharedKernel.Permissions.RoleCatalogue.IsStoredRole(role))
            {
                throw new InvalidOperationException($"'{role}' is not a stored role (Part 20.2).");
            }
        }

        var displayName = options.DisplayName ?? "Administrator";
        var user = Edm.Identity.Domain.UserAccount.Create(
            Guid.CreateVersion7(),
            tenant.TenantId,
            username,
            displayName,
            null,
            hasher.Hash(password),
            roles);

        await store.AddAsync(user, cancellationToken);
        await audit.WriteAsync(
            new AuditEventDraft
            {
                Module = "Identity",
                EntityType = "User",
                EntityId = user.Id.ToString(),
                Action = "UserCreated",
                After = new { user.Username, user.DisplayName, user.Roles },
            },
            cancellationToken);

        return true;
    }
}
