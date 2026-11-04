using System.Text.Json.Nodes;
using Edm.Audit.Application;
using Edm.Audit.Domain;
using Edm.SharedKernel.Context;
using Edm.UnitTests.Support;

namespace Edm.UnitTests.Application;

public sealed class AuditWriterTests
{
    private static readonly Guid Tenant = TenantDefaults.SingleTenant;
    private static readonly Guid UserId = Guid.Parse("5b0a1c2d-0000-4000-8000-000000000042");

    [Fact]
    public async Task The_writer_fills_time_tenant_actor_and_correlation_from_the_context()
    {
        var store = new FakeAuditStore();
        var writer = new AuditWriter(
            store,
            new FixedClock(new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.FromHours(5))),
            new FixedTenantContext(Tenant),
            new FixedActor(UserId, "Sardar Rafhy", "Administration", "EDM Administrator"),
            new FixedCorrelation("corr-abc-123"));

        await writer.WriteAsync(new AuditEventDraft
        {
            Module = "Identity",
            EntityType = "department",
            EntityId = "ADM",
            Action = "CREATE",
            After = new { Name = "Administration", Code = "ADM" },
        }, CancellationToken.None);

        var e = Assert.Single(store.Events);
        Assert.Equal(Tenant, e.TenantId);
        Assert.Equal(TimeSpan.Zero, e.OccurredAtUtc.Offset);
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 5, 0, 0, TimeSpan.Zero), e.OccurredAtUtc);
        Assert.Equal(UserId, e.ActorUserId);
        Assert.Equal("Sardar Rafhy", e.ActorName);
        Assert.Equal("Administration", e.ActorDepartment);
        Assert.Equal(new[] { "EDM Administrator" }, e.ActorRoles);
        Assert.Equal("corr-abc-123", e.CorrelationId);
        Assert.Equal("10.0.0.5", e.SourceIp);
        Assert.NotEqual(Guid.Empty, e.Id);
    }

    [Fact]
    public async Task A_create_stores_the_after_snapshot_and_one_change_per_field()
    {
        var store = new FakeAuditStore();
        var writer = Writer(store);

        await writer.WriteAsync(new AuditEventDraft
        {
            Module = "Identity",
            EntityType = "department",
            EntityId = "ADM",
            Action = "CREATE",
            After = new { Name = "Administration", Code = "ADM" },
        }, CancellationToken.None);

        var e = Assert.Single(store.Events);
        Assert.Null(e.BeforeJson);
        Assert.NotNull(e.AfterJson);
        Assert.Equal(2, JsonNode.Parse(e.ChangesJson!)!.AsArray().Count);
    }

    [Fact]
    public async Task An_update_stores_only_the_changed_field_in_the_diff()
    {
        var store = new FakeAuditStore();
        var writer = Writer(store);

        await writer.WriteAsync(new AuditEventDraft
        {
            Module = "Identity",
            EntityType = "department",
            EntityId = "ADM",
            Action = "UPDATE",
            Before = new { Name = "Administration", Code = "ADM" },
            After = new { Name = "Administration and GST", Code = "ADM" },
        }, CancellationToken.None);

        var e = Assert.Single(store.Events);
        var changes = JsonNode.Parse(e.ChangesJson!)!.AsArray();
        var change = Assert.Single(changes);
        Assert.Equal("name", change!["field"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_blank_module_is_refused_and_nothing_is_stored()
    {
        var store = new FakeAuditStore();
        var writer = Writer(store);

        await Assert.ThrowsAsync<Edm.SharedKernel.Errors.EdmException>(() => writer.WriteAsync(new AuditEventDraft
        {
            Module = " ",
            EntityType = "department",
            EntityId = "ADM",
            Action = "CREATE",
        }, CancellationToken.None));

        Assert.Empty(store.Events);
    }

    private static AuditWriter Writer(FakeAuditStore store) => new(
        store,
        new FixedClock(new DateTimeOffset(2026, 11, 4, 5, 0, 0, TimeSpan.Zero)),
        new FixedTenantContext(Tenant),
        new FixedActor(UserId, "Sardar Rafhy", "Administration", "EDM Administrator"),
        new FixedCorrelation("corr-test"));
}
