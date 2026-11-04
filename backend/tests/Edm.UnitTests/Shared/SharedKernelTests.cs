using System.Text.RegularExpressions;
using Edm.SharedKernel.Errors;
using Edm.SharedKernel.Permissions;
using Edm.Web.Authorization;

namespace Edm.UnitTests.Shared;

public sealed class PermissionCatalogueTests
{
    [Fact]
    public void The_catalogue_has_the_49_permissions_of_part_20_1()
    {
        Assert.Equal(49, PermissionCatalogue.All.Count);
    }

    [Fact]
    public void The_catalogue_has_no_duplicates()
    {
        Assert.Equal(PermissionCatalogue.All.Count, PermissionCatalogue.All.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_catalogue_matches_the_permission_matrix_document()
    {
        var path = FindRepoFile("docs/permission-matrix.md");
        var text = File.ReadAllText(path);

        // Only section 1 ("## 1. Permissions") is the catalogue. Its rows look like: | Group | `permission.string` | Meaning |
        var start = text.IndexOf("## 1. Permissions", StringComparison.Ordinal);
        var end = text.IndexOf("## 2.", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "docs/permission-matrix.md must contain section 1 and section 2.");
        var section = text[start..end];

        var documented = Regex.Matches(section, @"^\|\s*[^|]+\|\s*`([a-z]+(?:\.[a-z]+)+)`\s*\|", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToArray();

        Assert.Equal(PermissionCatalogue.All.OrderBy(p => p, StringComparer.Ordinal), documented.OrderBy(p => p, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("audit.view", true)]
    [InlineData("config.matrix.approve", true)]
    [InlineData("audit.everything", false)]
    [InlineData("AUDIT.VIEW", false)]
    public void IsKnown_accepts_only_catalogue_strings(string permission, bool expected)
    {
        Assert.Equal(expected, PermissionCatalogue.IsKnown(permission));
    }

    private static string FindRepoFile(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {relative} above {AppContext.BaseDirectory}.");
    }
}

public sealed class ErrorCodeCatalogueTests
{
    [Fact]
    public void The_catalogue_has_the_19_starter_codes_of_part_21_3()
    {
        Assert.Equal(19, EdmErrorCodes.All.Count);
    }

    [Fact]
    public void Every_code_has_the_EDM_prefix_and_is_unique()
    {
        var codes = EdmErrorCodes.All.Select(e => e.Code).ToArray();
        Assert.All(codes, code => Assert.StartsWith("EDM-", code));
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_messages_match_part_21_3_exactly()
    {
        Assert.Equal(
            "No approval level is configured for this amount. Please contact Admin or HR.",
            EdmErrorCodes.NoRouteForAmount.Message);
        Assert.Equal("Please sign in.", EdmErrorCodes.NotAuthenticated.Message);
        Assert.Equal("You do not have access to this.", EdmErrorCodes.NotPermitted.Message);
        Assert.Equal("This record was changed by someone else. Please reload.", EdmErrorCodes.StaleUpdate.Message);
    }

    [Fact]
    public void Authentication_and_concurrency_codes_have_their_HTTP_statuses()
    {
        Assert.Equal(401, EdmErrorCodes.NotAuthenticated.HttpStatus);
        Assert.Equal(403, EdmErrorCodes.NotPermitted.HttpStatus);
        Assert.Equal(412, EdmErrorCodes.StaleUpdate.HttpStatus);
        Assert.Equal(422, EdmErrorCodes.ValidationFailed.HttpStatus);
    }
}

public sealed class RequiresPermissionAttributeTests
{
    [Fact]
    public void A_known_permission_builds_the_policy_name()
    {
        var attribute = new RequiresPermissionAttribute("audit.view");

        Assert.Equal("audit.view", attribute.Permission);
        Assert.Equal("edm:permission:audit.view", attribute.Policy);
    }

    [Fact]
    public void An_unknown_permission_fails_when_the_attribute_is_built()
    {
        var ex = Assert.Throws<ArgumentException>(() => new RequiresPermissionAttribute("audit.everything"));

        Assert.Contains("audit.everything", ex.Message);
    }
}
