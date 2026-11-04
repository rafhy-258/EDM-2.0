using Edm.Audit.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Edm.Audit.Infrastructure;

public static class AuditInfrastructureServices
{
    public static IServiceCollection AddAuditInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<AuditDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAuditStore, EfAuditStore>();
        services.AddSingleton(new AuditSchemaMigrator(connectionString));
        return services;
    }
}
