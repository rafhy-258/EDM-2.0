using Npgsql;

namespace Edm.Audit.Infrastructure;

/// <summary>
/// Applies the embedded SQL migrations in name order, once each, recorded in schema_migrations.
/// Each script runs in its own transaction under an advisory lock, so two instances starting together
/// cannot apply the same script twice.
/// </summary>
/// <remarks>
/// Phase 0 uses versioned SQL for the audit schema, because the append-only trigger must be exact.
/// EF Core migrations are adopted for the business modules (Phase 1A onwards). See the Phase 0 notes.
/// </remarks>
public sealed class AuditSchemaMigrator(string connectionString)
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

            if (await IsAppliedAsync(connection, transaction, version, cancellationToken))
            {
                continue;
            }

            await using (var script = new NpgsqlCommand(sql, connection, transaction))
            {
                await script.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var record = new NpgsqlCommand(
                "INSERT INTO schema_migrations (version) VALUES (@version)", connection, transaction))
            {
                record.Parameters.AddWithValue("version", version);
                await record.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static async Task<bool> IsAppliedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string version,
        CancellationToken cancellationToken)
    {
        await using var check = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM schema_migrations WHERE version = @version)", connection, transaction);
        check.Parameters.AddWithValue("version", version);
        return (bool)(await check.ExecuteScalarAsync(cancellationToken))!;
    }

    internal static IReadOnlyList<(string Version, string Sql)> LoadScripts()
    {
        var assembly = typeof(AuditSchemaMigrator).Assembly;
        var scripts = new List<(string Version, string Sql)>();

        foreach (var name in assembly.GetManifestResourceNames()
                     .Where(n => n.EndsWith(".sql", StringComparison.Ordinal))
                     .OrderBy(n => n, StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Embedded migration '{name}' could not be read.");
            using var reader = new StreamReader(stream);
            scripts.Add((Path.GetFileNameWithoutExtension(name), reader.ReadToEnd()));
        }

        return scripts;
    }
}
