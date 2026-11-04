using Edm.Identity.Application;
using Edm.Identity.Infrastructure;

namespace Edm.UnitTests.Identity;

public sealed class PasswordHasherTests
{
    private static readonly Pbkdf2PasswordHasher Hasher = new(new IdentityOptions { PasswordIterations = 1000 });

    [Fact]
    public void A_hash_verifies_only_the_password_it_was_made_from()
    {
        var hash = Hasher.Hash("correct horse battery");

        Assert.True(Hasher.Verify("correct horse battery", hash));
        Assert.False(Hasher.Verify("wrong horse battery", hash));
    }

    [Fact]
    public void Two_hashes_of_the_same_password_differ_because_each_has_its_own_salt()
    {
        Assert.NotEqual(Hasher.Hash("same password here"), Hasher.Hash("same password here"));
    }

    [Fact]
    public void The_stored_format_records_the_algorithm_and_the_iteration_count()
    {
        var hash = Hasher.Hash("correct horse battery");

        Assert.StartsWith("PBKDF2-SHA256$1000$", hash, StringComparison.Ordinal);
        Assert.DoesNotContain("correct horse battery", hash, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("PBKDF2-SHA256$1000$AAAA$")]
    [InlineData("md5$1000$AAAA$AAAA")]
    [InlineData("PBKDF2-SHA256$0$AAAA$AAAA")]
    [InlineData("PBKDF2-SHA256$1000$not base64!$AAAA")]
    public void A_malformed_hash_never_verifies(string malformed)
    {
        Assert.False(Hasher.Verify("anything at all", malformed));
    }

    [Fact]
    public void The_dummy_verification_runs_without_throwing()
    {
        Hasher.ConsumeVerificationTime("anything at all");
    }
}
