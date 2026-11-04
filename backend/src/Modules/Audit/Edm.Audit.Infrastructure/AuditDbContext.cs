using Edm.Audit.Domain;
using Microsoft.EntityFrameworkCore;

namespace Edm.Audit.Infrastructure;

/// <summary>
/// EF Core mapping for audit events. Column names are explicit snake_case and match
/// Migrations/0001_audit_schema.sql. An integration test checks that the two agree.
/// </summary>
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(e => e.Id).HasName("pk_audit_events");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.OccurredAtUtc).HasColumnName("occurred_at_utc");
            entity.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(e => e.ActorName).HasColumnName("actor_name").HasMaxLength(200);
            entity.Property(e => e.ActorDepartment).HasColumnName("actor_department").HasMaxLength(200);
            entity.Property(e => e.ActorRoles).HasColumnName("actor_roles").HasColumnType("text[]");
            entity.Property(e => e.SourceIp).HasColumnName("source_ip").HasMaxLength(64);
            entity.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
            entity.Property(e => e.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128);
            entity.Property(e => e.Module).HasColumnName("module").HasMaxLength(64);
            entity.Property(e => e.EntityType).HasColumnName("entity_type").HasMaxLength(100);
            entity.Property(e => e.EntityId).HasColumnName("entity_id").HasMaxLength(200);
            entity.Property(e => e.Action).HasColumnName("action").HasMaxLength(100);
            entity.Property(e => e.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.FailureReason).HasColumnName("failure_reason").HasMaxLength(500);
            entity.Property(e => e.Remarks).HasColumnName("remarks").HasMaxLength(1000);
            entity.Property(e => e.RequestReference).HasColumnName("request_reference").HasMaxLength(64);
            entity.Property(e => e.PolicyVersion).HasColumnName("policy_version");
            entity.Property(e => e.BeforeJson).HasColumnName("before_json").HasColumnType("jsonb");
            entity.Property(e => e.AfterJson).HasColumnName("after_json").HasColumnType("jsonb");
            entity.Property(e => e.ChangesJson).HasColumnName("changes_json").HasColumnType("jsonb");
        });
    }
}
