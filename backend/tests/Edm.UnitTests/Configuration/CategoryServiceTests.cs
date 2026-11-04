using Edm.Audit.Application;
using Edm.Configuration.Application.Catalogue;
using Edm.SharedKernel.Context;
using Edm.SharedKernel.Errors;
using Edm.UnitTests.Support;

namespace Edm.UnitTests.Configuration;

public sealed class CategoryServiceTests
{
    internal static readonly Guid Tenant = TenantDefaults.SingleTenant;
    internal static readonly Guid Actor = Guid.Parse("5b0a1c2d-0000-4000-8000-000000000042");
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Everything the category and subcategory services need, wired to fakes.</summary>
    internal sealed class Rig
    {
        public Rig(FakeUsageChecker? usages = null)
        {
            Usages = usages ?? new FakeUsageChecker();
            var writer = new AuditWriter(
                Audit,
                new FixedClock(Now),
                new FixedTenantContext(Tenant),
                new FixedActor(Actor, "Test Admin", "Administration", "EDM_ADMINISTRATOR"),
                new FixedCorrelation("corr-config"));
            Categories = new CategoryService(Store, Usages, writer, new FixedTenantContext(Tenant), new FixedActor(Actor, "Test Admin", "Administration"), new FixedClock(Now));
            Subcategories = new SubcategoryService(Store, Usages, writer, new FixedTenantContext(Tenant), new FixedActor(Actor, "Test Admin", "Administration"), new FixedClock(Now));
        }

        public InMemoryCatalogueStore Store { get; } = new();

        public FakeAuditStore Audit { get; } = new();

        public FakeUsageChecker Usages { get; }

        public CategoryService Categories { get; }

        public SubcategoryService Subcategories { get; }
    }

    [Fact]
    public async Task Create_trims_the_name_and_starts_active_at_version_one()
    {
        var rig = new Rig();

        var created = await rig.Categories.CreateAsync(new CategoryCreate("  Travel  ", "  Flights and hotels  "), default);

        Assert.Equal("Travel", created.Category.Name);
        Assert.Equal("Flights and hotels", created.Category.Description);
        Assert.True(created.Category.IsActive);
        Assert.Equal(1, created.Category.RowVersion);
        Assert.Equal(0, created.SubcategoryCount);
        Assert.Equal(Actor, created.Category.UpdatedBy);
        Assert.Single(rig.Store.Categories);
    }

    [Fact]
    public async Task Duplicate_name_is_a_field_error_and_ignores_case_and_spaces()
    {
        var rig = new Rig();
        await rig.Categories.CreateAsync(new CategoryCreate("Travel", null), default);

        var error = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Categories.CreateAsync(new CategoryCreate(" travel ", null), default));

        Assert.Equal(EdmErrorCodes.ValidationFailed, error.Error);
        Assert.True(error.FieldErrors.ContainsKey("name"));
        Assert.Equal("A category with this name already exists.", error.FieldErrors["name"][0]);
        Assert.Single(rig.Store.Categories);
    }

    [Fact]
    public async Task Blank_and_too_long_values_are_field_errors_on_their_own_fields()
    {
        var rig = new Rig();

        var error = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Categories.CreateAsync(new CategoryCreate("   ", new string('d', 501)), default));

        Assert.Equal(EdmErrorCodes.ValidationFailed, error.Error);
        Assert.Equal("Enter a name.", error.FieldErrors["name"][0]);
        Assert.Equal("The description must be 500 characters or fewer.", error.FieldErrors["description"][0]);
        Assert.Empty(rig.Audit.Events);
    }

    [Fact]
    public async Task Each_change_writes_one_audit_event_with_old_and_new_values()
    {
        var rig = new Rig();
        var created = await rig.Categories.CreateAsync(new CategoryCreate("Travel", null), default);

        var renamed = await rig.Categories.UpdateAsync(
            created.Category.Id,
            new CategoryUpdate("Air travel", null, IsActive: true, RowVersion: 1),
            default);
        var deactivated = await rig.Categories.UpdateAsync(
            created.Category.Id,
            new CategoryUpdate("Air travel", null, IsActive: false, RowVersion: renamed!.Category.RowVersion),
            default);
        await rig.Categories.DeleteAsync(created.Category.Id, deactivated!.Category.RowVersion, default);

        var events = rig.Audit.Events;
        Assert.Equal(new[] { "Created", "Updated", "Deactivated", "Deleted" }, events.Select(e => e.Action).ToArray());
        Assert.All(events, e =>
        {
            Assert.Equal("Configuration", e.Module);
            Assert.Equal("Category", e.EntityType);
            Assert.Equal(created.Category.Id.ToString(), e.EntityId);
        });
        Assert.Null(events[0].BeforeJson);
        Assert.NotNull(events[1].BeforeJson);
        Assert.NotNull(events[1].AfterJson);
        Assert.NotNull(events[3].BeforeJson);
        Assert.Null(events[3].AfterJson);
    }

    [Fact]
    public async Task A_stale_row_version_is_refused_with_the_concurrency_code()
    {
        var rig = new Rig();
        var created = await rig.Categories.CreateAsync(new CategoryCreate("Travel", null), default);
        await rig.Categories.UpdateAsync(created.Category.Id, new CategoryUpdate("Air travel", null, true, 1), default);

        var error = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Categories.UpdateAsync(created.Category.Id, new CategoryUpdate("Rail", null, true, 1), default));

        Assert.Equal(EdmErrorCodes.StaleUpdate, error.Error);
        Assert.Equal("Air travel", rig.Store.Categories.Single().Name);
    }

    [Fact]
    public async Task An_omitted_active_flag_keeps_the_current_value()
    {
        var rig = new Rig();
        var created = await rig.Categories.CreateAsync(new CategoryCreate("Travel", null), default);

        var updated = await rig.Categories.UpdateAsync(
            created.Category.Id,
            new CategoryUpdate("Travel", "Updated text", IsActive: null, RowVersion: 1),
            default);

        Assert.True(updated!.Category.IsActive);
        Assert.Equal("Updated text", updated.Category.Description);
    }

    [Fact]
    public async Task A_deactivated_name_can_be_reused()
    {
        var rig = new Rig();
        var first = await rig.Categories.CreateAsync(new CategoryCreate("Travel", null), default);
        await rig.Categories.UpdateAsync(first.Category.Id, new CategoryUpdate("Travel", null, false, 1), default);

        var second = await rig.Categories.CreateAsync(new CategoryCreate("Travel", null), default);

        Assert.NotEqual(first.Category.Id, second.Category.Id);
        Assert.Equal(2, rig.Store.Categories.Count);
    }

    [Fact]
    public async Task Reactivating_into_a_name_that_is_now_taken_is_refused()
    {
        var rig = new Rig();
        var first = await rig.Categories.CreateAsync(new CategoryCreate("Travel", null), default);
        await rig.Categories.UpdateAsync(first.Category.Id, new CategoryUpdate("Travel", null, false, 1), default);
        await rig.Categories.CreateAsync(new CategoryCreate("Travel", null), default);

        var error = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Categories.UpdateAsync(first.Category.Id, new CategoryUpdate("Travel", null, true, 2), default));

        Assert.True(error.FieldErrors.ContainsKey("name"));
    }

    [Fact]
    public async Task A_category_with_subcategories_cannot_be_deleted_even_when_they_are_inactive()
    {
        var rig = new Rig();
        var category = await rig.Categories.CreateAsync(new CategoryCreate("Travel", null), default);
        var sub = await rig.Subcategories.CreateAsync(new SubcategoryCreate(category.Category.Id, "Flights", null), default);
        await rig.Subcategories.UpdateAsync(sub.Subcategory.Id, new SubcategoryUpdate("Flights", null, false, 1), default);

        var error = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Categories.DeleteAsync(category.Category.Id, 1, default));

        Assert.Equal(EdmErrorCodes.RecordInUse, error.Error);
        Assert.Single(rig.Store.Categories);
    }

    [Fact]
    public async Task The_usage_checker_is_asked_before_a_delete_and_a_usage_blocks_it()
    {
        var usages = new FakeUsageChecker
        {
            Usages = [new ConfigurationUsage("Rule", "Travel rule for Department A")],
        };
        var rig = new Rig(usages);
        var category = await rig.Categories.CreateAsync(new CategoryCreate("Travel", null), default);

        var error = await Assert.ThrowsAsync<EdmException>(() =>
            rig.Categories.DeleteAsync(category.Category.Id, 1, default));

        Assert.Equal(EdmErrorCodes.RecordInUse, error.Error);
        Assert.Equal(new[] { ConfigurationEntity.Category }, usages.Asked.ToArray());
        Assert.Single(rig.Store.Categories);
        Assert.DoesNotContain(rig.Audit.Events, e => e.Action == "Deleted");
    }

    [Fact]
    public async Task Deleting_an_unused_category_is_a_soft_delete_that_hides_it()
    {
        var rig = new Rig();
        var category = await rig.Categories.CreateAsync(new CategoryCreate("Travel", null), default);

        var deleted = await rig.Categories.DeleteAsync(category.Category.Id, 1, default);

        Assert.True(deleted);
        Assert.Empty(await rig.Categories.ListAsync(includeInactive: true, default));
        Assert.Single(rig.Store.Categories);
    }

    [Fact]
    public async Task Missing_categories_are_reported_without_an_error()
    {
        var rig = new Rig();

        Assert.Null(await rig.Categories.UpdateAsync(Guid.NewGuid(), new CategoryUpdate("X", null, true, 1), default));
        Assert.False(await rig.Categories.DeleteAsync(Guid.NewGuid(), 1, default));
    }

    [Fact]
    public async Task Inactive_categories_are_listed_only_when_asked()
    {
        var rig = new Rig();
        var active = await rig.Categories.CreateAsync(new CategoryCreate("Travel", null), default);
        var inactive = await rig.Categories.CreateAsync(new CategoryCreate("Old", null), default);
        await rig.Categories.UpdateAsync(inactive.Category.Id, new CategoryUpdate("Old", null, false, 1), default);

        var onlyActive = await rig.Categories.ListAsync(includeInactive: false, default);
        var everything = await rig.Categories.ListAsync(includeInactive: true, default);

        Assert.Equal(new[] { active.Category.Id }, onlyActive.Select(c => c.Category.Id).ToArray());
        Assert.Equal(2, everything.Count);
    }
}
