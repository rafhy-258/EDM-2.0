using Npgsql;
using Testcontainers.PostgreSql;

namespace Edm.IntegrationTests.Support;

/// <summary>
/// One PostgreSQL server for the whole test run. It is either:
///   - the server named by the EDM_TEST_POSTGRES environment variable (for example a local PostgreSQL 18), or
///   - a Testcontainers container, which needs Docker.
/// Every test class gets its own freshly created database, which is dropped afterwards.
/// </summary>
public sealed class PostgresServer : IAsyncLifetime
{
    public const string ExternalVariable = "EDM_TEST_POSTGRES";

    private PostgreSqlContainer? _container;

    public string AdminConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable(ExternalVariable);
        if (!string.IsNullOrWhiteSpace(external))
        {
            AdminConnectionString = external;
            return;
        }

        _container = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
        await _container.StartAsync();
        AdminConnectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public async Task<PostgresTestDatabase> CreateDatabaseAsync()
    {
        var name = $"edm_test_{Guid.NewGuid():N}";

        await using (var admin = new NpgsqlConnection(AdminConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = name };
        return new PostgresTestDatabase(AdminConnectionString, name, builder.ConnectionString);
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresServer>
{
    public const string Name = "postgres";
}
