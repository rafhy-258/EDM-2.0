using Edm.Configuration.Domain.Authorities;
using Xunit;

namespace Edm.UnitTests.Configuration;

public sealed class ApprovalAuthorityRulesTests
{
    [Theory]
    [InlineData("Finance Review", null)]
    [InlineData("Finance  Review 2", null)]
    [InlineData("", "Enter a title.")]
    [InlineData("Finance & Accounts", "Use letters, numbers and spaces only.")]
    [InlineData("Finance-Review", "Use letters, numbers and spaces only.")]
    [InlineData("Finance\nReview", "Use letters, numbers and spaces only.")]
    public void Validates_recommending_authority_titles(string? title, string? expectedError)
        => Assert.Equal(expectedError, ApprovalAuthorityRules.TitleError(title));

    [Fact]
    public void Normalises_title_spacing()
        => Assert.Equal("Finance Review 2", ApprovalAuthorityRules.NormaliseTitle("  Finance   Review 2  "));

    [Fact]
    public void Rejects_titles_longer_than_200_characters()
        => Assert.Equal("The title must be 200 characters or fewer.", ApprovalAuthorityRules.TitleError(new string('A', 201)));

    [Fact]
    public void Rejects_forward_for_ec_members()
        => Assert.Equal(
            "EC members cannot be assigned the Forward action.",
            ApprovalAuthorityRules.ActionSetError(
                ApprovalDesignationCodes.EcMember,
                [ApprovalActionCodes.Approve, ApprovalActionCodes.Forward]));

    [Fact]
    public void Allows_forward_only_for_department_head()
        => Assert.Null(ApprovalAuthorityRules.ActionSetError(
            ApprovalDesignationCodes.DepartmentHead,
            ApprovalActionCodes.All));

    [Fact]
    public void Requires_approve_for_every_action_set()
        => Assert.Equal(
            "Approve must remain available.",
            ApprovalAuthorityRules.ActionSetError(
                ApprovalDesignationCodes.Cfo,
                [ApprovalActionCodes.Reject]));
}
