using System.Text.Json.Nodes;
using Edm.Audit.Domain;

namespace Edm.UnitTests.Domain;

public sealed class AuditDiffTests
{
    [Fact]
    public void Identical_snapshots_have_no_changes()
    {
        var before = JsonNode.Parse("""{"name":"A","rate":"1.0000"}""");
        var after = JsonNode.Parse("""{"name":"A","rate":"1.0000"}""");

        Assert.Empty(AuditDiff.Compute(before, after));
    }

    [Fact]
    public void Changed_top_level_field_is_reported_with_before_and_after()
    {
        var before = JsonNode.Parse("""{"name":"A","rate":"1.0000"}""");
        var after = JsonNode.Parse("""{"name":"B","rate":"1.0000"}""");

        var change = Assert.Single(AuditDiff.Compute(before, after));

        Assert.Equal("name", change.Field);
        Assert.Equal("A", change.Before!.GetValue<string>());
        Assert.Equal("B", change.After!.GetValue<string>());
    }

    [Fact]
    public void Nested_fields_use_dotted_paths_and_only_changed_leaves_are_listed()
    {
        var before = JsonNode.Parse("""{"amount":100,"address":{"city":"Karachi","zip":"74000"}}""");
        var after = JsonNode.Parse("""{"amount":150,"address":{"city":"Karachi","zip":"75500"}}""");

        var changes = AuditDiff.Compute(before, after).Select(c => c.Field).ToArray();

        Assert.Equal(new[] { "address.zip", "amount" }, changes);
    }

    [Fact]
    public void A_create_lists_every_field_as_a_change_from_nothing()
    {
        var after = JsonNode.Parse("""{"code":"USD","rate":"280.5000"}""");

        var changes = AuditDiff.Compute(null, after);

        Assert.Equal(2, changes.Count);
        Assert.All(changes, c => Assert.Null(c.Before));
    }

    [Fact]
    public void A_delete_lists_every_field_as_a_change_to_nothing()
    {
        var before = JsonNode.Parse("""{"code":"USD"}""");

        var change = Assert.Single(AuditDiff.Compute(before, null));

        Assert.Equal("code", change.Field);
        Assert.Null(change.After);
    }

    [Fact]
    public void Both_sides_null_gives_no_changes()
    {
        Assert.Empty(AuditDiff.Compute(null, null));
    }

    [Fact]
    public void A_scalar_replaced_by_an_object_is_one_change_at_that_field()
    {
        var before = JsonNode.Parse("""{"limit":"20000"}""");
        var after = JsonNode.Parse("""{"limit":{"amount":"20000"}}""");

        var change = Assert.Single(AuditDiff.Compute(before, after));

        Assert.Equal("limit", change.Field);
        Assert.Equal("20000", change.Before!.GetValue<string>());
        Assert.NotNull(change.After!["amount"]);
    }
}
