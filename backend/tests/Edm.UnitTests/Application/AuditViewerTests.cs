using Edm.Audit.Application;
using Edm.Audit.Domain;
using Edm.SharedKernel.Context;
using Edm.SharedKernel.Errors;
using Edm.UnitTests.Support;

namespace Edm.UnitTests.Application;

public sealed class AuditViewerTests
{
    private static readonly Guid Tenant = TenantDefaults.SingleTenant;

    private static (AuditViewer viewer, FakeAuditStore store) Build()
    {
        var store = new FakeAuditStore();
        var writer = new AuditWriter(
            store,
            new FixedClock(new DateTimeOffset(2026, 11, 4, 5, 0, 0, TimeSpan.Zero)),
            new FixedTenantContext(Tenant),
            new FixedActor(Guid.NewGuid(), "Auditor One", "Compliance", "Auditor / Compliance"),
            new FixedCorrelation("corr-view"));
        return (new AuditViewer(store, writer), store);
    }

    private static AuditQuery Query(int page = 1, int pageSize = 50, DateTimeOffset? from = null, DateTimeOffset? to = null)
        => new(from, to, null, null, null, null, null, null, page, pageSize);

    [Fact]
    public async Task Every_view_of_the_audit_store_is_itself_written_to_the_audit_store()
    {
        var (viewer, store) = Build();

        await viewer.ListAsync(Query(), CancellationToken.None);

        var view = Assert.Single(store.Events);
        Assert.Equal("VIEW", view.Action);
        Assert.Equal("Audit", view.Module);
        Assert.Equal("Auditor One", view.ActorName);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 201)]
    public async Task Invalid_paging_is_refused_with_field_errors(int page, int pageSize)
    {
        var (viewer, store) = Build();

        var ex = await Assert.ThrowsAsync<EdmException>(() => viewer.ListAsync(Query(page, pageSize), CancellationToken.None));

        Assert.Equal(EdmErrorCodes.ValidationFailed.Code, ex.Error.Code);
        Assert.Empty(store.Events);
    }

    [Fact]
    public async Task An_end_date_before_the_start_date_is_refused()
    {
        var (viewer, _) = Build();
        var start = new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero);

        var ex = await Assert.ThrowsAsync<EdmException>(() =>
            viewer.ListAsync(Query(from: start, to: start.AddDays(-1)), CancellationToken.None));

        Assert.Contains("to", ex.FieldErrors.Keys);
    }

    [Fact]
    public async Task Overlong_filter_values_are_refused()
    {
        var (viewer, _) = Build();
        var query = new AuditQuery(null, null, new string('x', 65), null, null, null, null, null, 1, 10);

        var ex = await Assert.ThrowsAsync<EdmException>(() => viewer.ListAsync(query, CancellationToken.None));

        Assert.Contains("module", ex.FieldErrors.Keys);
    }
}
