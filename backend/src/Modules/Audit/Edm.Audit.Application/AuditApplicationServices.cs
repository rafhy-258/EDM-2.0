using Microsoft.Extensions.DependencyInjection;

namespace Edm.Audit.Application;

public static class AuditApplicationServices
{
    public static IServiceCollection AddAuditApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<AuditViewer>();
        return services;
    }
}
