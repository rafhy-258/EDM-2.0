using Edm.Configuration.Application.Catalogue;
using Edm.Configuration.Infrastructure.Catalogue;
using Microsoft.Extensions.DependencyInjection;

namespace Edm.Configuration.Infrastructure;

public static class ConfigurationInfrastructureServices
{
    public static IServiceCollection AddConfigurationInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<ICatalogueStore>(new NpgsqlCatalogueStore(connectionString));
        services.AddSingleton(new ConfigurationSchemaMigrator(connectionString));
        return services;
    }
}
