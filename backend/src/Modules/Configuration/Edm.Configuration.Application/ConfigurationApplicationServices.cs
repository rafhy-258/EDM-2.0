using Edm.Configuration.Application.Catalogue;
using Edm.Configuration.Application.Limits;
using Microsoft.Extensions.DependencyInjection;

namespace Edm.Configuration.Application;

public static class ConfigurationApplicationServices
{
    public static IServiceCollection AddConfigurationApplication(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationUsageChecker, NoConfigurationUsages>();
        services.AddScoped<CategoryService>();
        services.AddScoped<SubcategoryService>();
        services.AddScoped<DeptHeadLimitService>();
        return services;
    }
}
