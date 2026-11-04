using Edm.Audit.Application;
using Edm.Audit.Domain;
using Edm.Identity.Application;
using Edm.Identity.Domain;
using Edm.SharedKernel.Context;
using Edm.SharedKernel.Permissions;
using Edm.UnitTests.Support;

namespace Edm.UnitTests.Identity;

public sealed class SignInServiceTests
{
    private static readonly Guid Tenant = TenantDefaults.SingleTenant;
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 10, 0, 0, TimeSpan.FromHours(5));

    [Fact]
    public async Task A_correct_password_signs_in_and_writes_a_success_event()
    {
        var rig = Rig.Create();
        rig.Store.Add(rig.User("sara", "pw-correct-1", RoleCatalogue.Auditor));

        var result = await rig.Service.SignInAsync("  Sara ", "pw-correct-1", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("sara", result.Session!.Username);
        Assert.Contains("audit.view", result.Session.Permissions);
        var written = Assert.Single(rig.Audit.Events);
        Assert.Equal("SignIn", written.Action);
        Assert.Equal(AuditOutcome.Success, written.Outcome);
        Assert.Equal("Identity", written.Module);
    }

    [Fact]
    public async Task A_wrong_password_is_rejected_with_an_audited_reason_and_no_session()
    {
        var rig = Rig.Create();
        rig.Store.Add(rig.User("sara", "pw-correct-1", RoleCatalogue.Auditor));

        var result = await rig.Service.SignInAsync("sara", "not-the-password", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.Session);
        var written = Assert.Single(rig.Audit.Events);
        Assert.Equal("SignInFailed", written.Action);
        Assert.Equal(AuditOutcome.Failure, written.Outcome);
        Assert.Equal("Invalid password", written.FailureReason);
    }

    [Fact]
    public async Task An_unknown_username_is_rejected_after_a_verification_and_is_audited()
    {
        var rig = Rig.Create();

        var result = await rig.Service.SignInAsync("nobody", "anything-at-all", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(1, rig.Hasher.DummyVerifications);
        var written = Assert.Single(rig.Audit.Events);
        Assert.Equal("Unknown username", written.FailureReason);
    }

    [Fact]
    public async Task A_disabled_account_is_refused_without_checking_the_password()
    {
        var rig = Rig.Create();
        rig.Store.Add(rig.User("sara", "pw-correct-1", RoleCatalogue.Auditor, active: false));

        var result = await rig.Service.SignInAsync("sara", "pw-correct-1", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(0, rig.Hasher.Verifications);
        Assert.Equal("Account is disabled", Assert.Single(rig.Audit.Events).FailureReason);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_and_the_correct_password_is_then_refused_unchecked()
    {
        var rig = Rig.Create();
        rig.Store.Add(rig.User("sara", "pw-correct-1", RoleCatalogue.Auditor));

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await rig.Service.SignInAsync("sara", "wrong", CancellationToken.None);
            Assert.False(failed.Succeeded);
        }

        var verificationsBefore = rig.Hasher.Verifications;
        var locked = await rig.Service.SignInAsync("sara", "pw-correct-1", CancellationToken.None);

        Assert.False(locked.Succeeded);
        Assert.Equal(verificationsBefore, rig.Hasher.Verifications);
        Assert.Equal(1, rig.Audit.Events.Count(e => e.Action == "AccountLocked"));
        Assert.Equal(6, rig.Audit.Events.Count(e => e.Action == "SignInFailed"));
    }

    [Fact]
    public async Task A_successful_sign_in_resets_the_failure_count_saved_to_the_store()
    {
        var rig = Rig.Create();
        var user = rig.User("sara", "pw-correct-1", RoleCatalogue.Auditor);
        rig.Store.Add(user);
        await rig.Service.SignInAsync("sara", "wrong", CancellationToken.None);

        var result = await rig.Service.SignInAsync("sara", "pw-correct-1", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(0, user.FailedSignIns);
        // One save for the failed attempt and one for the successful sign-in.
        Assert.Equal(2, rig.Store.SavedStates);
    }

    [Fact]
    public async Task The_bootstrap_creates_the_first_administrator_once_and_audits_it()
    {
        var rig = Rig.Create();
        var bootstrap = rig.Bootstrap();
        var options = new BootstrapOptions { Username = "Admin.One", Password = "a-long-first-password" };

        var created = await bootstrap.EnsureAsync(options, CancellationToken.None);
        var createdAgain = await bootstrap.EnsureAsync(options, CancellationToken.None);

        Assert.True(created);
        Assert.False(createdAgain);
        var account = Assert.Single(rig.Store.Users.Values);
        Assert.Equal("admin.one", account.Username);
        Assert.Equal(new[] { RoleCatalogue.SystemAdministrator, RoleCatalogue.Auditor }, account.Roles.ToArray());
        Assert.Equal("UserCreated", Assert.Single(rig.Audit.Events).Action);
    }

    [Fact]
    public async Task The_bootstrap_refuses_a_short_password_and_an_unknown_role()
    {
        var rig = Rig.Create();
        var bootstrap = rig.Bootstrap();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            bootstrap.EnsureAsync(new BootstrapOptions { Username = "admin", Password = "short" }, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            bootstrap.EnsureAsync(
                new BootstrapOptions { Username = "admin", Password = "a-long-first-password", Roles = "WIZARD" },
                CancellationToken.None));

        Assert.Empty(rig.Store.Users);
    }

    [Fact]
    public async Task The_bootstrap_does_nothing_without_a_username()
    {
        var rig = Rig.Create();

        var created = await rig.Bootstrap().EnsureAsync(new BootstrapOptions(), CancellationToken.None);

        Assert.False(created);
        Assert.Empty(rig.Store.Users);
    }

    private sealed class Rig
    {
        private Rig(FakeUserStore store, FakePasswordHasher hasher, FakeAuditStore audit, SignInService service, AuditWriter writer)
        {
            Store = store;
            Hasher = hasher;
            Audit = audit;
            Service = service;
            Writer = writer;
        }

        public FakeUserStore Store { get; }

        public FakePasswordHasher Hasher { get; }

        public FakeAuditStore Audit { get; }

        public SignInService Service { get; }

        private AuditWriter Writer { get; }

        public static Rig Create()
        {
            var store = new FakeUserStore();
            var hasher = new FakePasswordHasher();
            var audit = new FakeAuditStore();
            var writer = new AuditWriter(
                audit,
                new FixedClock(Now),
                new FixedTenantContext(Tenant),
                new FixedActor(null, "Anonymous", null),
                new FixedCorrelation("corr-sign-in"));
            var service = new SignInService(
                store,
                hasher,
                writer,
                new FixedTenantContext(Tenant),
                new FixedClock(Now),
                new LockoutPolicy(5, TimeSpan.FromMinutes(15)));
            return new Rig(store, hasher, audit, service, writer);
        }

        public UserAccount User(string username, string password, string role, bool active = true) =>
            UserAccount.Restore(
                Guid.NewGuid(),
                Tenant,
                username,
                "Test " + username,
                "Finance",
                "hash:" + password,
                active,
                0,
                null,
                new[] { role });

        public BootstrapAdministrator Bootstrap() => new(
            Store,
            Hasher,
            Writer,
            new FixedTenantContext(Tenant),
            new PasswordPolicy(12));
    }

    private sealed class FakeUserStore : IUserAccountStore
    {
        public Dictionary<string, UserAccount> Users { get; } = new(StringComparer.Ordinal);

        public int SavedStates { get; private set; }

        public void Add(UserAccount user) => Users[user.Username] = user;

        public Task<UserAccount?> FindByUsernameAsync(Guid tenantId, string username, CancellationToken cancellationToken)
            => Task.FromResult(Users.TryGetValue(username, out var user) ? user : null);

        public Task<bool> AnyAsync(Guid tenantId, CancellationToken cancellationToken)
            => Task.FromResult(Users.Count > 0);

        public Task AddAsync(UserAccount user, CancellationToken cancellationToken)
        {
            Add(user);
            return Task.CompletedTask;
        }

        public Task SaveSignInStateAsync(UserAccount user, CancellationToken cancellationToken)
        {
            SavedStates++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public int Verifications { get; private set; }

        public int DummyVerifications { get; private set; }

        public string Hash(string password) => "hash:" + password;

        public bool Verify(string password, string passwordHash)
        {
            Verifications++;
            return passwordHash == "hash:" + password;
        }

        public void ConsumeVerificationTime(string password) => DummyVerifications++;
    }
}
