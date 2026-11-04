using System.Text.Json.Nodes;
using Edm.Audit.Application;
using Edm.Audit.Domain;
using Edm.Audit.Infrastructure;
using Edm.IntegrationTests.Support;
using Edm.SharedKernel.Context;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Edm.IntegrationTests;

/// <summary>Real PostgreSQL: schema, round trip, append-only guarantees and the application role's grants.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AuditStoreTests(PostgresServer server) : IAsyncLifetime
{
    private static readonly Guid Tenant = TenantDefaults.SingleTenant;
    private static readonly DateTimeOffset Instant = new(2026, 11, 4, 5, 0, 0, TimeSpan.Zero);

    private PostgresTestDatabase _db = null!;

    public async Task InitializeAsync()
    {
        _db = await server.CreateDatabaseAsync();
        await new AuditSchemaMigrator(_db.ConnectionString).MigrateAsync(CancellationToken.None);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Migrations_apply_once_and_a_second_run_changes_nothing()
    {
        await new AuditSchemaMigrator(_db.ConnectionString).MigrateAsync(CancellationToken.None);

        var count = await ScalarAsync(_db.ConnectionString, "SELECT COUNT(*) FROM schema_migrations");
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task An_event_round_trips_through_the_store_with_every_field_intact()
    {
        var written = NewEvent(JsonNode.Parse("""{"rate":"1.0000"}"""), JsonNode.Parse("""{"rate":"1.0500"}"""));

        await using (var db = Context())
        {
            var store = new EfAuditStore(db, new FixedTenantContext(Tenant));
            await store.AppendAsync(written, CancellationToken.None);
        }

        await using var readDb = Context();
        var read = new EfAuditStore(readDb, new FixedTenantContext(Tenant));
        var page = await read.QueryAsync(new AuditQuery(null, null, "Configuration", null, null, null, null, null, 1, 50), CancellationToken.None);

        var e = Assert.Single(page.Items);
        Assert.Equal(written.Id, e.Id);
        Assert.Equal(Instant, e.OccurredAtUtc);
        Assert.Equal(new[] { "EDM Administrator", "Auditor" }, e.ActorRoles);
        Assert.Equal("Sardar Rafhy", e.ActorName);
        Assert.Equal("corr-int-1", e.CorrelationId);
        Assert.Equal(AuditOutcome.Success, e.Outcome);
        Assert.Equal(1, e.PolicyVersion);
        Assert.Equal("1.0000", JsonNode.Parse(e.BeforeJson!)!["rate"]!.GetValue<string>());
        Assert.Equal("rate", JsonNode.Parse(e.ChangesJson!)!.AsArray()[0]!["field"]!.GetValue<string>());
    }

    [Fact]
    public async Task Queries_filter_and_page_and_count_the_matching_rows()
    {
        await using (var db = Context())
        {
            var store = new EfAuditStore(db, new FixedTenantContext(Tenant));
            for (var i = 0; i < 3; i++)
            {
                await store.AppendAsync(NewEvent(module: "Configuration"), CancellationToken.None);
            }

            await store.AppendAsync(NewEvent(module: "Identity"), CancellationToken.None);
        }

        await using var readDb = Context();
        var read = new EfAuditStore(readDb, new FixedTenantContext(Tenant));

        var configuration = await read.QueryAsync(new AuditQuery(null, null, "Configuration", null, null, null, null, null, 1, 2), CancellationToken.None);

        Assert.Equal(3, configuration.TotalCount);
        Assert.Equal(2, configuration.Items.Count);
        Assert.All(configuration.Items, e => Assert.Equal("Configuration", e.Module));
    }

    [Fact]
    public async Task A_failure_with_no_reason_is_refused_by_the_database_as_well()
    {
        var sql = """
            INSERT INTO audit_events (id, tenant_id, occurred_at_utc, actor_name, correlation_id, module, entity_type, entity_id, action, outcome)
            VALUES (@id, @tenant, @at, 'Anonymous', 'corr', 'Identity', 'sign-in', 'x', 'SIGN_IN', 'Failure')
            """;

        var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var connection = new NpgsqlConnection(_db.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("tenant", Tenant);
            command.Parameters.AddWithValue("at", Instant);
            await command.ExecuteNonQueryAsync();
        });

        Assert.Equal("23514", ex.SqlState);
    }

    [Fact]
    public async Task The_owner_cannot_update_an_audit_event()
    {
        await AppendOneAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(_db.ConnectionString, "UPDATE audit_events SET action = 'TAMPERED'"));

        Assert.Equal("P0001", ex.SqlState);
        Assert.Contains("append-only", ex.MessageText);
    }

    [Fact]
    public async Task The_owner_cannot_delete_an_audit_event()
    {
        await AppendOneAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(_db.ConnectionString, "DELETE FROM audit_events"));

        Assert.Equal("P0001", ex.SqlState);
    }

    [Fact]
    public async Task Nobody_can_truncate_the_audit_table()
    {
        await AppendOneAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(_db.ConnectionString, "TRUNCATE audit_events"));

        Assert.Equal("P0001", ex.SqlState);
        Assert.Equal(1L, await ScalarAsync(_db.ConnectionString, "SELECT COUNT(*) FROM audit_events"));
    }

    [Fact]
    public async Task The_application_role_can_append_and_read_but_cannot_change_or_delete()
    {
        var appConnection = await _db.CreateApplicationRoleAsync("app-role-pw-0001");

        await using (var db = new AuditDbContext(new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(appConnection).Options))
        {
            var store = new EfAuditStore(db, new FixedTenantContext(Tenant));
            await store.AppendAsync(NewEvent(), CancellationToken.None);
        }

        var update = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(appConnection, "UPDATE audit_events SET action = 'X'"));
        Assert.Equal("42501", update.SqlState);

        var delete = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(appConnection, "DELETE FROM audit_events"));
        Assert.Equal("42501", delete.SqlState);
    }

    private async Task AppendOneAsync()
    {
        await using var db = Context();
        var store = new EfAuditStore(db, new FixedTenantContext(Tenant));
        await store.AppendAsync(NewEvent(), CancellationToken.None);
    }

    private AuditDbContext Context()
        => new(new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(_db.ConnectionString).Options);

    private static AuditEvent NewEvent(JsonNode? before = null, JsonNode? after = null, string module = "Configuration")
    {
        return AuditEvent.Create(new AuditEventSpec
        {
            Id = Guid.CreateVersion7(),
            TenantId = Tenant,
            OccurredAtUtc = Instant,
            ActorUserId = Guid.Parse("5b0a1c2d-0000-4000-8000-000000000042"),
            ActorName = "Sardar Rafhy",
            ActorDepartment = "Administration",
            ActorRoles = ["EDM Administrator", "Auditor"],
            SourceIp = "10.0.0.5",
            UserAgent = "integration-test",
            CorrelationId = "corr-int-1",
            Module = module,
            EntityType = "currency",
            EntityId = "PKR",
            Action = "UPDATE",
            Outcome = AuditOutcome.Success,
            Remarks = "rate change",
            PolicyVersion = 1,
            Before = before,
            After = after,
        });
    }

    private static async Task<object?> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync();
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
