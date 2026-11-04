using Edm.Audit.Domain;
using Edm.SharedKernel.Paging;

namespace Edm.Audit.Application;

/// <summary>Persistence port for audit events. Implemented in Infrastructure. Has no update or delete.</summary>
public interface IAuditStore
{
    Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

    Task<PagedResult<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// The single write service for audit events (ADR-0005). Every module writes through this interface.
/// </summary>
public interface IAuditWriter
{
    Task WriteAsync(AuditEventDraft draft, CancellationToken cancellationToken);
}
