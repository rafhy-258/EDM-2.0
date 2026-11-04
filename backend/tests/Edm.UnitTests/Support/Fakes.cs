using Edm.Audit.Application;
using Edm.Audit.Domain;
using Edm.SharedKernel.Context;
using Edm.SharedKernel.Paging;

namespace Edm.UnitTests.Support;

internal sealed class FakeAuditStore : IAuditStore
{
    public List<AuditEvent> Events { get; } = [];

    public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        Events.Add(auditEvent);
        return Task.CompletedTask;
    }

    public Task<PagedResult<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        var items = Events
            .Where(e => query.Module is null || e.Module == query.Module)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();
        return Task.FromResult(new PagedResult<AuditEvent>(items, query.Page, query.PageSize, Events.Count));
    }
}

internal sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow => now;
}

internal sealed class FixedActor(Guid? userId, string name, string? department, params string[] roles) : IActorContext
{
    public Guid? UserId => userId;

    public string DisplayName => name;

    public string? Department => department;

    public IReadOnlyList<string> Roles => roles;

    public string? SourceIp => "10.0.0.5";

    public string? UserAgent => "unit-test-agent";
}

internal sealed class FixedCorrelation(string id) : ICorrelationIdAccessor
{
    public string CorrelationId => id;
}
