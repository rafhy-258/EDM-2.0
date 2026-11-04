using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Edm.Host.Health;

/// <summary>Readiness check: the database accepts a connection and answers a trivial query.</summary>
public sealed class DatabaseHealthCheck(IConfiguration configuration) : IHealthCheck
{
    private readonly string _connectionString = configuration.GetConnectionString("Edm")
        ?? throw new InvalidOperationException("ConnectionStrings:Edm is not set.");

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("The database accepts connections.");
        }
        catch (NpgsqlException exception)
        {
            // The detail is kept out of the response. It is available in the exception for logging.
            return HealthCheckResult.Unhealthy("The database is not reachable.", exception);
        }
    }
}
