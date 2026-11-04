using Npgsql;

namespace Edm.IntegrationTests.Support;

/// <summary>A test database. Disposing it drops the database and any login roles created for it.</summary>
public sealed class PostgresTestDatabase(string adminConnectionString, string name, string connectionString)
    : IAsyncDisposable
{
    private readonly List<string> _roles = [];

    public string Name { get; } = name;

    public string ConnectionString { get; } = connectionString;

    /// <summary>
    /// Creates a login role that can connect to this database and is granted only what the application
    /// role needs on audit_events: INSERT and SELECT. Returns a connection string that uses the role.
    /// </summary>
    public async Task<string> CreateApplicationRoleAsync(string password)
    {
        var role = $"edm_app_{Guid.NewGuid():N}";

        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await ExecuteAsync(admin, $"CREATE ROLE \"{role}\" LOGIN PASSWORD '{password}'");
        }

        await using (var owner = new NpgsqlConnection(ConnectionString))
        {
            await owner.OpenAsync();
            await ExecuteAsync(owner, $"GRANT CONNECT ON DATABASE \"{Name}\" TO \"{role}\"");
            await ExecuteAsync(owner, $"GRANT USAGE ON SCHEMA public TO \"{role}\"");
            await ExecuteAsync(owner, $"GRANT SELECT, INSERT ON audit_events TO \"{role}\"");
        }

        _roles.Add(role);

        var builder = new NpgsqlConnectionStringBuilder(ConnectionString) { Username = role, Password = password };
        return builder.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)");
        foreach (var role in _roles)
        {
            await ExecuteAsync(admin, $"DROP ROLE IF EXISTS \"{role}\"");
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
