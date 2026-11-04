using Edm.Audit.Application;
using Edm.Audit.Domain;
using Edm.SharedKernel.Context;
using Edm.SharedKernel.Paging;
using Microsoft.EntityFrameworkCore;

namespace Edm.Audit.Infrastructure;

/// <summary>EF Core implementation of the audit store. Append and read only. Every read is scoped to the tenant.</summary>
public sealed class EfAuditStore(AuditDbContext db, ITenantContext tenant) : IAuditStore
{
    public async Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        if (auditEvent.TenantId != tenant.TenantId)
        {
            throw new InvalidOperationException("The audit event belongs to a different tenant from the current context.");
        }

        db.AuditEvents.Add(auditEvent);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var tenantId = tenant.TenantId;
        IQueryable<AuditEvent> events = db.AuditEvents.AsNoTracking().Where(e => e.TenantId == tenantId);

        if (query.From is { } from)
        {
            events = events.Where(e => e.OccurredAtUtc >= from);
        }

        if (query.To is { } to)
        {
            events = events.Where(e => e.OccurredAtUtc <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            events = events.Where(e => e.Module == query.Module);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            events = events.Where(e => e.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            events = events.Where(e => e.EntityType == query.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            events = events.Where(e => e.EntityId == query.EntityId);
        }

        if (query.ActorUserId is { } actor)
        {
            events = events.Where(e => e.ActorUserId == actor);
        }

        if (query.Outcome is { } outcome)
        {
            events = events.Where(e => e.Outcome == outcome);
        }

        var total = await events.CountAsync(cancellationToken);

        var items = await events
            .OrderByDescending(e => e.OccurredAtUtc)
            .ThenByDescending(e => e.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditEvent>(items, query.Page, query.PageSize, total);
    }
}
