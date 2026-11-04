using Edm.Configuration.Application.Catalogue;
using Microsoft.Extensions.DependencyInjection;

namespace Edm.Configuration.Application;

public static class ConfigurationApplicationServices
{
    public static IServiceCollection AddConfigurationApplication(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationUsageChecker, NoConfigurationUsages>();
        services.AddScoped<CategoryService>();
        services.AddScoped<SubcategoryService>();
        return services;
    }
}
