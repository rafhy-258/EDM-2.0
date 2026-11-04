using Edm.Audit.Api;
using Edm.Audit.Application;
using Edm.Audit.Infrastructure;
using Edm.Host.Health;
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
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ITenantContext>(new FixedTenantContext(tenantId));
builder.Services.AddScoped<IActorContext, HttpActorContext>();
builder.Services.AddScoped<ICorrelationIdAccessor, HttpCorrelationIdAccessor>();

// Deny by default (ADR-0006). No sign-in until SSO is configured. Every endpoint needs a permission or AllowAnonymous.
builder.Services
    .AddAuthentication(EdmAuthenticationDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, DenyByDefaultAuthenticationHandler>(EdmAuthenticationDefaults.Scheme, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Modules.
builder.Services.AddAuditInfrastructure(connectionString);
builder.Services.AddAuditApplication();

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.GetRequiredService<AuditSchemaMigrator>().MigrateAsync(CancellationToken.None);
}

app.Run();

/// <summary>Exposed for the integration tests (WebApplicationFactory).</summary>
public partial class Program
{
}
