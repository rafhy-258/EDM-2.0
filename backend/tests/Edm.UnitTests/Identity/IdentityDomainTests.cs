using Edm.Identity.Domain;
using Edm.SharedKernel.Context;
using Edm.SharedKernel.Permissions;

namespace Edm.UnitTests.Identity;

public sealed class IdentityDomainTests
{
    private static readonly Guid Tenant = TenantDefaults.SingleTenant;
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 10, 0, 0, TimeSpan.FromHours(5));
    private static readonly LockoutPolicy Policy = new(3, TimeSpan.FromMinutes(15));

    private static UserAccount NewUser() =>
        UserAccount.Create(Guid.NewGuid(), Tenant, "sara", "Sara Khan", "Finance", "hash", new[] { RoleCatalogue.Auditor });

    [Fact]
    public void The_account_locks_at_the_threshold_and_stays_locked_until_the_lock_expires()
    {
        var user = NewUser();

        Assert.False(user.RecordFailedSignIn(Now, Policy));
        Assert.False(user.RecordFailedSignIn(Now, Policy));
        Assert.True(user.RecordFailedSignIn(Now, Policy));

        Assert.True(user.IsLockedAt(Now.AddMinutes(14)));
        Assert.False(user.IsLockedAt(Now.AddMinutes(15)));
    }

    [Fact]
    public void A_successful_sign_in_clears_the_failure_count_and_any_lock()
    {
        var user = NewUser();
        user.RecordFailedSignIn(Now, Policy);
        user.RecordFailedSignIn(Now, Policy);

        user.RecordSuccessfulSignIn();

        Assert.Equal(0, user.FailedSignIns);
        Assert.Null(user.LockedUntilUtc);
        Assert.False(user.RecordFailedSignIn(Now, Policy));
    }

    [Fact]
    public void After_a_lock_expires_the_failure_count_starts_again()
    {
        var user = NewUser();
        user.RecordFailedSignIn(Now, Policy);
        user.RecordFailedSignIn(Now, Policy);
        user.RecordFailedSignIn(Now, Policy);

        var later = Now.AddMinutes(16);

        Assert.False(user.IsLockedAt(later));
        Assert.False(user.RecordFailedSignIn(later, Policy));
        Assert.Equal(1, user.FailedSignIns);
    }

    [Fact]
    public void Create_rejects_a_role_that_is_not_stored()
    {
        Assert.Throws<ArgumentException>(() =>
            UserAccount.Create(Guid.NewGuid(), Tenant, "sara", "Sara", null, "hash", new[] { "NOT_A_ROLE" }));
    }

    [Fact]
    public void Lockout_policy_rejects_a_threshold_below_one_and_a_zero_duration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LockoutPolicy(0, TimeSpan.FromMinutes(15)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LockoutPolicy(5, TimeSpan.Zero));
    }

    [Fact]
    public void Password_policy_requires_the_minimum_length_and_refuses_the_username()
    {
        var policy = new PasswordPolicy(12);

        Assert.Contains(policy.Validate("sara", "short"), p => p.Contains("at least 12"));
        Assert.Contains(policy.Validate("sara", "my-SARA-password-1"), p => p.Contains("must not contain the username"));
        Assert.Empty(policy.Validate("sara", "a long enough passphrase"));
    }

    [Fact]
    public void Password_policy_refuses_a_blank_password_and_a_password_over_the_maximum()
    {
        var policy = new PasswordPolicy(12);

        Assert.Single(policy.Validate("sara", "            "));
        Assert.Contains(policy.Validate("sara", new string('x', PasswordPolicy.MaximumLength + 1)), p => p.Contains("at most"));
    }

    [Fact]
    public void Password_policy_minimum_must_be_at_least_eight()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PasswordPolicy(7));
    }

    [Theory]
    [InlineData("sara", true)]
    [InlineData("sara.khan_1", true)]
    [InlineData("ab", false)]
    [InlineData("sara khan", false)]
    [InlineData("sara@edm", false)]
    public void Usernames_are_three_to_sixty_four_safe_characters(string username, bool valid)
    {
        Assert.Equal(valid, UsernameRules.IsValid(username));
    }

    [Fact]
    public void Usernames_are_trimmed_and_lower_cased()
    {
        Assert.Equal("sara.khan", UsernameRules.Normalise("  Sara.Khan "));
    }
}
