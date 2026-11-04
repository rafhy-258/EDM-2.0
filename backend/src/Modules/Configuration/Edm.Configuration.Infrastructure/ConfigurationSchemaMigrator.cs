using Npgsql;

namespace Edm.Configuration.Infrastructure;

/// <summary>
/// Applies the embedded configuration SQL migrations in name order, once each, recorded in schema_migrations (the table
/// shared with the audit and identity migrators). Each script runs in its own transaction under the same advisory lock.
/// </summary>
public sealed class ConfigurationSchemaMigrator(string connectionString)
{
    private const string LockKey = "edm.schema_migrations";

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var create = new NpgsqlCommand(
            "CREATE TABLE IF NOT EXISTS schema_migrations (version text PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now())",
            connection))
        {
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var (version, sql) in LoadScripts())
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await using (var lockCommand = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext(@key))", connection, transaction))
            {
                lockCommand.Parameters.AddWithValue("key", LockKey);
                await lockCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            bool applied;
            await using (var check = new NpgsqlCommand(
                "SELECT EXISTS (SELECT 1 FROM schema_migrations WHERE version = @version)",
                connection,
                transaction))
            {
                check.Parameters.AddWithValue("version", version);
                applied = (bool)(await check.ExecuteScalarAsync(cancellationToken))!;
            }

            if (applied)
            {
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            await using (var script = new NpgsqlCommand(sql, connection, transaction))
            {
                await script.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var record = new NpgsqlCommand(
                "INSERT INTO schema_migrations (version) VALUES (@version)",
                connection,
                transaction))
            {
                record.Parameters.AddWithValue("version", version);
                await record.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
    }

    internal static IReadOnlyList<(string Version, string Sql)> LoadScripts()
    {
        var assembly = typeof(ConfigurationSchemaMigrator).Assembly;
        var scripts = new List<(string Version, string Sql)>();

        foreach (var name in assembly.GetManifestResourceNames()
                     .Where(n => n.EndsWith(".sql", StringComparison.Ordinal))
                     .OrderBy(n => n, StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"The embedded migration '{name}' could not be read.");
            using var reader = new StreamReader(stream);
            scripts.Add((Path.GetFileNameWithoutExtension(name), reader.ReadToEnd()));
        }

        return scripts;
    }
}
