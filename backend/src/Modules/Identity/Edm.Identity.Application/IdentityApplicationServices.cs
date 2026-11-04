using Edm.Identity.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Edm.Identity.Application;

public static class IdentityApplicationServices
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services, IdentityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddSingleton(new PasswordPolicy(options.MinimumPasswordLength));
        services.AddSingleton(new LockoutPolicy(options.LockoutThreshold, TimeSpan.FromMinutes(options.LockoutMinutes)));
        services.AddScoped<SignInService>();
        services.AddScoped<BootstrapAdministrator>();
        return services;
    }
}
