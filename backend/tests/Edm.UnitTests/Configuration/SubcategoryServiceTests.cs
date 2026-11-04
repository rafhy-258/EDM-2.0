using Edm.Configuration.Application.Catalogue;
using Edm.SharedKernel.Errors;
using Edm.UnitTests.Support;

namespace Edm.UnitTests.Configuration;

public sealed class SubcategoryServiceTests
{
    [Fact]
    public async Task The_same_name_is_allowed_under_another_category()
    {
        var rig = new CategoryServiceTests.Rig();
        var travel = await rig.Categories.CreateAsync(new CategoryCreate("Travel", null), default);
        var training = await rig.Categories.CreateAsync(new CategoryCreate("Training", null), default);

        await rig.Subcategories.CreateAsync(new SubcategoryCreate(travel.Category.Id, "Other", null), default);
        var second = await rig.Subcategories.CreateAsync(new SubcategoryCreate(training.Category.Id, "Other", null), default);

        Assert.Equal("Training", second.CategoryName);
        Assert.Equal(2, rig.Store.Subcategories.Count);
    }

    [Fact]
    public async Task The_same_name_under_the_same_category_is_a_field_error()
    {
        var rig = new CategoryServiceTests.Rig();
        var travel = await rig.Categories.CreateAsync(new CategoryCreate("Travel", null), default);
        await rig.Subcategories.CreateAsync(new SubcategoryCreate(travel.Category.Id, "Flights", null), default);

        var error = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Subcategories.CreateAsync(new SubcategoryCreate(travel.Category.Id, " flights ", null), default));

        Assert.Equal(EdmErrorCodes.ValidationFailed, error.Error);
        Assert.Equal(
            "A subcategory with this name already exists in this category.",
            error.FieldErrors["name"][0]);
    }

    [Fact]
    public async Task A_subcategory_needs_an_active_category()
    {
        var rig = new CategoryServiceTests.Rig();
        var inactive = await rig.Categories.CreateAsync(new CategoryCreate("Old", null), default);
        await rig.Categories.UpdateAsync(inactive.Category.Id, new CategoryUpdate("Old", null, false, 1), default);

        var missing = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Subcategories.CreateAsync(new SubcategoryCreate(null, "Flights", null), default));
        var deactivated = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Subcategories.CreateAsync(new SubcategoryCreate(inactive.Category.Id, "Flights", null), default));

        Assert.True(missing.FieldErrors.ContainsKey("categoryId"));
        Assert.True(deactivated.FieldErrors.ContainsKey("categoryId"));
        Assert.Empty(rig.Store.Subcategories);
    }

    [Fact]
    public async Task A_subcategory_stays_under_its_category_when_it_is_renamed()
    {
        var rig = new CategoryServiceTests.Rig();
        var travel = await rig.Categories.CreateAsync(new CategoryCreate("Travel", null), default);
        var sub = await rig.Subcategories.CreateAsync(new SubcategoryCreate(travel.Category.Id, "Flights", null), default);

        var updated = await rig.Subcategories.UpdateAsync(
            sub.Subcategory.Id,
            new SubcategoryUpdate("Air flights", null, IsActive: null, RowVersion: 1),
            default);

        Assert.Equal(travel.Category.Id, updated!.Subcategory.CategoryId);
        Assert.Equal("Air flights", updated.Subcategory.Name);
        Assert.True(updated.Subcategory.IsActive);
    }

    [Fact]
    public async Task A_stale_subcategory_update_is_refused()
    {
        var rig = new CategoryServiceTests.Rig();
        var travel = await rig.Categories.CreateAsync(new CategoryCreate("Travel", null), default);
        var sub = await rig.Subcategories.CreateAsync(new SubcategoryCreate(travel.Category.Id, "Flights", null), default);
        await rig.Subcategories.UpdateAsync(sub.Subcategory.Id, new SubcategoryUpdate("Flights", null, true, 1), default);

        var error = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Subcategories.UpdateAsync(sub.Subcategory.Id, new SubcategoryUpdate("Rail", null, true, 1), default));

        Assert.Equal(EdmErrorCodes.StaleUpdate, error.Error);
    }

    [Fact]
    public async Task Deleting_a_used_subcategory_is_refused_and_the_usage_is_checked()
    {
        var usages = new FakeUsageChecker
        {
            Usages = [new ConfigurationUsage("Request", "OPEX-2026-000001")],
        };
        var rig = new CategoryServiceTests.Rig(usages);
        var travel = await rig.Categories.CreateAsync(new CategoryCreate("Travel", null), default);
        var sub = await rig.Subcategories.CreateAsync(new SubcategoryCreate(travel.Category.Id, "Flights", null), default);

        var error = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Subcategories.DeleteAsync(sub.Subcategory.Id, 1, default));

        Assert.Equal(EdmErrorCodes.RecordInUse, error.Error);
        Assert.Equal(new[] { ConfigurationEntity.Subcategory }, usages.Asked.ToArray());
    }

    [Fact]
    public async Task Each_subcategory_change_is_audited_with_the_subcategory_entity_type()
    {
        var rig = new CategoryServiceTests.Rig();
        var travel = await rig.Categories.CreateAsync(new CategoryCreate("Travel", null), default);
        var sub = await rig.Subcategories.CreateAsync(new SubcategoryCreate(travel.Category.Id, "Flights", null), default);
        await rig.Subcategories.UpdateAsync(sub.Subcategory.Id, new SubcategoryUpdate("Flights", null, false, 1), default);
        await rig.Subcategories.DeleteAsync(sub.Subcategory.Id, 2, default);

        var subEvents = rig.Audit.Events.Where(e => e.EntityType == "Subcategory").Select(e => e.Action).ToArray();
        Assert.Equal(new[] { "Created", "Deactivated", "Deleted" }, subEvents);
    }

    [Fact]
    public async Task Missing_subcategories_are_reported_without_an_error()
    {
        var rig = new CategoryServiceTests.Rig();

        Assert.Null(await rig.Subcategories.UpdateAsync(Guid.NewGuid(), new SubcategoryUpdate("X", null, true, 1), default));
        Assert.False(await rig.Subcategories.DeleteAsync(Guid.NewGuid(), 1, default));
    }
}
