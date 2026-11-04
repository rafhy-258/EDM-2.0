using System.Text.Json.Nodes;
using Edm.Audit.Domain;
using Edm.SharedKernel.Errors;

namespace Edm.UnitTests.Domain;

public sealed class AuditEventTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static AuditEventSpec ValidSpec() => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = Tenant,
        OccurredAtUtc = new DateTimeOffset(2026, 11, 4, 5, 0, 0, TimeSpan.Zero),
        ActorName = "Sardar Rafhy",
        ActorRoles = ["EDM Administrator"],
        CorrelationId = "corr-1",
        Module = "Configuration",
        EntityType = "currency",
        EntityId = "PKR",
        Action = "UPDATE",
        PolicyVersion = 1,
        Before = JsonNode.Parse("""{"rate":"1.0000"}"""),
        After = JsonNode.Parse("""{"rate":"1.0500"}"""),
    };

    [Fact]
    public void A_valid_spec_creates_an_event_with_the_diff_stored()
    {
        var e = AuditEvent.Create(ValidSpec());

        Assert.Equal("UPDATE", e.Action);
        Assert.Equal(AuditOutcome.Success, e.Outcome);
        Assert.NotNull(e.ChangesJson);
        var changes = JsonNode.Parse(e.ChangesJson!)!.AsArray();
        Assert.Single(changes);
        Assert.Equal("rate", changes[0]!["field"]!.GetValue<string>());
    }

    [Fact]
    public void Times_are_stored_in_UTC()
    {
        var spec = ValidSpec();
        var e = AuditEvent.Create(new AuditEventSpec
        {
            Id = spec.Id,
            TenantId = spec.TenantId,
            OccurredAtUtc = new DateTimeOffset(2026, 11, 4, 10, 0, 0, TimeSpan.FromHours(5)),
            ActorName = spec.ActorName,
            CorrelationId = spec.CorrelationId,
            Module = spec.Module,
            EntityType = spec.EntityType,
            EntityId = spec.EntityId,
            Action = spec.Action,
            PolicyVersion = 1,
        });

        Assert.Equal(TimeSpan.Zero, e.OccurredAtUtc.Offset);
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 5, 0, 0, TimeSpan.Zero), e.OccurredAtUtc);
    }

    [Fact]
    public void A_failure_without_a_reason_is_rejected()
    {
        var failing = new AuditEventSpec
        {
            Id = Guid.CreateVersion7(),
            TenantId = Tenant,
            OccurredAtUtc = new DateTimeOffset(2026, 11, 4, 5, 0, 0, TimeSpan.Zero),
            ActorName = "Anonymous",
            CorrelationId = "corr-2",
            Module = "Identity",
            EntityType = "sign-in",
            EntityId = "user@example.com",
            Action = "SIGN_IN",
            Outcome = AuditOutcome.Failure,
        };

        var ex = Assert.Throws<EdmException>(() => AuditEvent.Create(failing));

        Assert.Equal(EdmErrorCodes.ValidationFailed.Code, ex.Error.Code);
        Assert.Contains("failureReason", ex.FieldErrors.Keys);
    }

    [Fact]
    public void Missing_required_text_is_reported_per_field()
    {
        var broken = new AuditEventSpec
        {
            Id = Guid.CreateVersion7(),
            TenantId = Tenant,
            OccurredAtUtc = new DateTimeOffset(2026, 11, 4, 5, 0, 0, TimeSpan.Zero),
            ActorName = " ",
            CorrelationId = "",
            Module = "",
            EntityType = "x",
            EntityId = "x",
            Action = "x",
            PolicyVersion = 1,
        };

        var ex = Assert.Throws<EdmException>(() => AuditEvent.Create(broken));

        Assert.Contains("actorName", ex.FieldErrors.Keys);
        Assert.Contains("correlationId", ex.FieldErrors.Keys);
        Assert.Contains("module", ex.FieldErrors.Keys);
    }

    [Fact]
    public void Values_longer_than_the_column_limit_are_rejected()
    {
        var tooLong = new AuditEventSpec
        {
            Id = Guid.CreateVersion7(),
            TenantId = Tenant,
            OccurredAtUtc = new DateTimeOffset(2026, 11, 4, 5, 0, 0, TimeSpan.Zero),
            ActorName = "A",
            CorrelationId = "c",
            Module = new string('m', 65),
            EntityType = "x",
            EntityId = "x",
            Action = "x",
            PolicyVersion = 1,
        };

        var ex = Assert.Throws<EdmException>(() => AuditEvent.Create(tooLong));

        Assert.Contains("module", ex.FieldErrors.Keys);
    }

    [Fact]
    public void Policy_version_must_be_at_least_one_when_given()
    {
        var zero = new AuditEventSpec
        {
            Id = Guid.CreateVersion7(),
            TenantId = Tenant,
            OccurredAtUtc = new DateTimeOffset(2026, 11, 4, 5, 0, 0, TimeSpan.Zero),
            ActorName = "A",
            CorrelationId = "c",
            Module = "m",
            EntityType = "x",
            EntityId = "x",
            Action = "x",
            PolicyVersion = 0,
        };

        var ex = Assert.Throws<EdmException>(() => AuditEvent.Create(zero));

        Assert.Contains("policyVersion", ex.FieldErrors.Keys);
    }
}
