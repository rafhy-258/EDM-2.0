using Edm.Configuration.Application.Catalogue;
using Edm.Configuration.Application.Limits;
using Edm.Configuration.Infrastructure.Catalogue;
using Edm.Configuration.Infrastructure.Limits;
using Edm.Configuration.Application.Currencies;
using Edm.Configuration.Infrastructure.Currencies;
using Microsoft.Extensions.DependencyInjection;

namespace Edm.Configuration.Infrastructure;

public static class ConfigurationInfrastructureServices
{
    public static IServiceCollection AddConfigurationInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<ICatalogueStore>(new NpgsqlCatalogueStore(connectionString));
        services.AddSingleton<IDeptHeadLimitStore>(new NpgsqlDeptHeadLimitStore(connectionString));
        services.AddSingleton<ICurrencyStore>(new NpgsqlCurrencyStore(connectionString));
        services.AddSingleton(new ConfigurationSchemaMigrator(connectionString));
        return services;
    }
}
