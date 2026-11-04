using Edm.Audit.Api;
using Edm.Audit.Application;
using Edm.Audit.Infrastructure;
using Edm.Host.Health;
using Edm.Identity.Api;
using Edm.Identity.Application;
using Edm.Identity.Domain;
using Edm.Identity.Infrastructure;
using Edm.Host.Middleware;
using Edm.SharedKernel.Context;
using Edm.Web.Authentication;
using Edm.Web.Authorization;
using Edm.Web.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}"));

var connectionString = builder.Configuration.GetConnectionString("Edm");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Edm is not set. Set the environment variable ConnectionStrings__Edm (see backend/README.md).");
}

var tenantText = builder.Configuration["Tenant:Id"];
var tenantId = string.IsNullOrWhiteSpace(tenantText) ? TenantDefaults.SingleTenant : Guid.Parse(tenantText);

// Errors and problem details (Part 21.1). The exception handler maps EdmException to its catalogue code.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<EdmExceptionHandler>();

// Context used by audit and authorisation.
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IClock, Edm.SharedKernel.Context.SystemClock>();
builder.Services.AddSingleton<ITenantContext>(new FixedTenantContext(tenantId));
builder.Services.AddScoped<IActorContext, HttpActorContext>();
builder.Services.AddScoped<ICorrelationIdAccessor, HttpCorrelationIdAccessor>();

// Local accounts with a session cookie (ADR-0006). A request without a valid session is denied. Every endpoint
// needs a permission, or is one of the explicit anonymous endpoints (sign-in, sign-out, session, health).
var identityOptions = builder.Configuration.GetSection("Identity").Get<IdentityOptions>() ?? new IdentityOptions();
builder.Services
    .AddAuthentication(EdmAuthenticationDefaults.SessionScheme)
    .AddEdmSession(TimeSpan.FromMinutes(identityOptions.IdleTimeoutMinutes));

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Modules.
builder.Services.AddAuditInfrastructure(connectionString);
builder.Services.AddAuditApplication();
builder.Services.AddIdentityInfrastructure(connectionString);
builder.Services.AddIdentityApplication(identityOptions);

// Health (Part 2, observability).
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" });

// OpenAPI document (development only). Each endpoint's required permission is checked by a test (ADR-0007).
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .AllowAnonymous();

app.MapAuditEndpoints();
app.MapIdentityEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.GetRequiredService<AuditSchemaMigrator>().MigrateAsync(CancellationToken.None);
    await app.Services.GetRequiredService<IdentitySchemaMigrator>().MigrateAsync(CancellationToken.None);

    // Creates the first administrator on a new database. Does nothing when an account already exists.
    using var bootstrapScope = app.Services.CreateScope();
    await bootstrapScope.ServiceProvider.GetRequiredService<BootstrapAdministrator>()
        .EnsureAsync(identityOptions.Bootstrap, CancellationToken.None);
}

app.Run();

/// <summary>Exposed for the integration tests (WebApplicationFactory).</summary>
public partial class Program
{
}
