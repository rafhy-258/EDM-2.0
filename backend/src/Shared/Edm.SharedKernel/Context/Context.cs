namespace Edm.SharedKernel.Context;

/// <summary>Source of the current instant. Injected so tests can fix time.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>The tenant every business row belongs to (ADR-0004: tenant_id on every table).</summary>
public interface ITenantContext
{
    Guid TenantId { get; }
}

public sealed class FixedTenantContext(Guid tenantId) : ITenantContext
{
    public Guid TenantId { get; } = tenantId;
}

public static class TenantDefaults
{
    /// <summary>The single tenant of the first deployments (Part 1.2). Configurable through Tenant:Id.</summary>
    public static readonly Guid SingleTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
}

/// <summary>
/// The acting user for audit purposes. Anonymous when there is no signed-in user.
/// Implemented from the HTTP request in the Web layer.
/// </summary>
public interface IActorContext
{
    Guid? UserId { get; }

    string DisplayName { get; }

    string? Department { get; }

    IReadOnlyList<string> Roles { get; }

    string? SourceIp { get; }

    string? UserAgent { get; }
}

/// <summary>Correlation id of the current unit of work (Part 2, observability).</summary>
public interface ICorrelationIdAccessor
{
    string CorrelationId { get; }
}
