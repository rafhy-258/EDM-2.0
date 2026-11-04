using Edm.Configuration.Domain.Catalogue;

namespace Edm.UnitTests.Configuration;

public sealed class CatalogueRulesTests
{
    [Fact]
    public void A_name_is_required()
    {
        Assert.Equal("Enter a name.", CatalogueRules.NameError(null));
        Assert.Equal("Enter a name.", CatalogueRules.NameError(string.Empty));
        Assert.Equal("Enter a name.", CatalogueRules.NameError("   "));
    }

    [Fact]
    public void A_name_of_200_characters_is_accepted_and_201_is_refused()
    {
        Assert.Null(CatalogueRules.NameError(new string('a', 200)));
        Assert.Equal(
            "The name must be 200 characters or fewer.",
            CatalogueRules.NameError(new string('a', 201)));
    }

    [Fact]
    public void Surrounding_spaces_do_not_count_towards_the_length()
    {
        Assert.Null(CatalogueRules.NameError("   " + new string('a', 200) + "   "));
    }

    [Fact]
    public void A_description_of_500_characters_is_accepted_and_501_is_refused()
    {
        Assert.Null(CatalogueRules.DescriptionError(null));
        Assert.Null(CatalogueRules.DescriptionError(new string('d', 500)));
        Assert.Equal(
            "The description must be 500 characters or fewer.",
            CatalogueRules.DescriptionError(new string('d', 501)));
    }

    [Fact]
    public void The_stored_name_is_trimmed_and_an_empty_description_becomes_null()
    {
        Assert.Equal("Travel", CatalogueRules.NormaliseName("  Travel  "));
        Assert.Null(CatalogueRules.NormaliseDescription("   "));
        Assert.Null(CatalogueRules.NormaliseDescription(null));
        Assert.Equal("Flights", CatalogueRules.NormaliseDescription("  Flights  "));
    }
}
