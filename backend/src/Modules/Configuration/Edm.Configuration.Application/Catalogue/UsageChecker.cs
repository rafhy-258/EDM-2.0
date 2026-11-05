namespace Edm.Configuration.Application.Catalogue;

/// <summary>The kinds of configuration record the usage check can ask about.</summary>
public enum ConfigurationEntity
{
    Category,
    Subcategory,
    Currency,
    EcMember,
    RecommendingAuthority,
}

/// <summary>One place that uses a configuration record, for example an active rule. Shown to the user before a change.</summary>
public sealed record ConfigurationUsage(string Kind, string Reference);

/// <summary>
/// Reports where a configuration record is used (data-model design note section 8). A record that is used cannot be
/// deleted, and deactivating it can show the list. Rules and requests do not exist until Phase 1B and 1C, so the stub
/// below reports no usages.
/// </summary>
public interface IConfigurationUsageChecker
{
    Task<IReadOnlyList<ConfigurationUsage>> FindUsagesAsync(
        ConfigurationEntity entity,
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken);
}

/// <summary>Stub until the rule and request modules exist (Phase 1B and 1C). Always reports no usages.</summary>
public sealed class NoConfigurationUsages : IConfigurationUsageChecker
{
    public Task<IReadOnlyList<ConfigurationUsage>> FindUsagesAsync(
        ConfigurationEntity entity,
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ConfigurationUsage> none = [];
        return Task.FromResult(none);
    }
}
