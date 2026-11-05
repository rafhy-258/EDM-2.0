using Edm.Audit.Application;
using Edm.Configuration.Application.Catalogue;
using Edm.Configuration.Domain.Limits;
using Edm.SharedKernel.Context;
using Edm.SharedKernel.Errors;

namespace Edm.Configuration.Application.Limits;

/// <summary>
/// Department-head limits (Master Prompt 5.5). One limit per nature is active at a time. A change closes the active row
/// and inserts a new one, so history is kept. Every change writes audit events with old and new values.
/// Maker-checker and policy versions (M1A-06) are not built yet. Changes apply when saved.
/// </summary>
public sealed class DeptHeadLimitService(
    IDeptHeadLimitStore store,
    IAuditWriter audit,
    ITenantContext tenant,
    IActorContext actor,
    IClock clock)
{
    private const string EntityType = "DeptHeadLimit";

    public async Task<DeptHeadLimitSummary> GetSummaryAsync(string? nature, CancellationToken cancellationToken)
    {
        var parsed = ParseNature(nature);
        var rows = await store.ListAsync(tenant.TenantId, parsed, cancellationToken);
        return new DeptHeadLimitSummary(parsed, DeptHeadLimitTimeline.Summarise(rows, clock.UtcNow), rows);
    }

    public async Task<DeptHeadLimit> SaveAsync(DeptHeadLimitSave input, CancellationToken cancellationToken)
    {
        var nature = ParseNature(input.Nature);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        AddError(errors, "amountPkr", DeptHeadLimitRules.AmountError(input.AmountPkr));
        AddError(errors, "description", DeptHeadLimitRules.DescriptionError(input.Description));

        var today = DeptHeadLimitRules.KarachiDate(clock.UtcNow);
        if (input.EffectiveDate is null)
        {
            AddError(errors, "effectiveDate", "Choose an effective date.");
        }
        else if (input.EffectiveDate.Value < today)
        {
            AddError(errors, "effectiveDate", "The effective date cannot be in the past.");
        }

        ThrowIfInvalid(errors);

        var rows = await store.ListAsync(tenant.TenantId, nature, cancellationToken);
        var open = rows.FirstOrDefault(r => r.IsActive);

        if (open is not null && input.EffectiveDate!.Value < DeptHeadLimitRules.KarachiDate(open.EffectiveFrom))
        {
            var openStart = DeptHeadLimitRules.KarachiDate(open.EffectiveFrom).ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
            AddError(errors, "effectiveDate", $"The effective date cannot be earlier than the current limit's start date ({openStart}).");
            ThrowIfInvalid(errors);
        }

        var expectedVersion = open?.RowVersion;
        if (open is null ? input.RowVersion is not null : input.RowVersion != open.RowVersion)
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        var now = clock.UtcNow;
        var start = DeptHeadLimitRules.StartOfKarachiDay(input.EffectiveDate!.Value);

        // Row versions must never repeat within one nature. A client holding the version of an earlier row must not
        // match a later open row. So the new row takes a version above every row so far, including the one it closes.
        var highest = rows.Count == 0 ? 0L : rows.Max(r => r.RowVersion);
        var created = new DeptHeadLimit(
            Guid.CreateVersion7(),
            tenant.TenantId,
            nature,
            input.AmountPkr!.Value,
            DeptHeadLimitRules.NormaliseDescription(input.Description),
            start,
            EffectiveTo: null,
            IsActive: true,
            RowVersion: Math.Max(highest, (open?.RowVersion ?? 0L) + 1L) + 1L,
            UpdatedAtUtc: now,
            UpdatedBy: actor.UserId);

        var closing = open is null
            ? null
            : open with
            {
                EffectiveTo = start,
                IsActive = false,
                RowVersion = open.RowVersion + 1,
                UpdatedAtUtc = now,
                UpdatedBy = actor.UserId,
            };

        if (!await store.ReplaceAsync(tenant.TenantId, nature, closing, expectedVersion, created, cancellationToken))
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        if (open is not null)
        {
            await audit.WriteAsync(
                CatalogueAudit.Draft(EntityType, open.Id, "Replaced", Snapshot(open), Snapshot(closing!)),
                cancellationToken);
        }

        await audit.WriteAsync(
            CatalogueAudit.Draft(EntityType, created.Id, "Created", before: null, after: Snapshot(created)),
            cancellationToken);

        return created;
    }

    /// <summary>
    /// Ends the active limit now. Returns false when there is no active limit. After this the seeded default applies.
    /// </summary>
    public async Task<bool> DeactivateAsync(string? nature, long rowVersion, CancellationToken cancellationToken)
    {
        var parsed = ParseNature(nature);
        var rows = await store.ListAsync(tenant.TenantId, parsed, cancellationToken);
        var open = rows.FirstOrDefault(r => r.IsActive);
        if (open is null)
        {
            return false;
        }

        if (open.RowVersion != rowVersion)
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        var now = clock.UtcNow;
        var closed = open with
        {
            IsActive = false,
            EffectiveTo = now > open.EffectiveFrom ? now : open.EffectiveFrom,
            RowVersion = open.RowVersion + 1,
            UpdatedAtUtc = now,
            UpdatedBy = actor.UserId,
        };

        if (!await store.DeactivateAsync(tenant.TenantId, parsed, closed, rowVersion, cancellationToken))
        {
            throw new EdmException(EdmErrorCodes.StaleUpdate);
        }

        await audit.WriteAsync(
            CatalogueAudit.Draft(EntityType, open.Id, "Deactivated", Snapshot(open), Snapshot(closed)),
            cancellationToken);

        return true;
    }

    private static LimitNature ParseNature(string? nature)
    {
        if (LimitNatureCodes.TryParse(nature, out var parsed))
        {
            return parsed;
        }

        throw new EdmException(
            EdmErrorCodes.ValidationFailed,
            new Dictionary<string, string[]> { ["nature"] = new[] { "Choose OPEX or CAPEX." } });
    }

    private static DeptHeadLimitSnapshot Snapshot(DeptHeadLimit limit)
        => new(
            LimitNatureCodes.Code(limit.Nature),
            limit.AmountPkr,
            limit.Description,
            limit.EffectiveFrom,
            limit.EffectiveTo,
            limit.IsActive);

    private static void AddError(Dictionary<string, string[]> errors, string field, string? message)
    {
        if (message is not null)
        {
            errors[field] = [message];
        }
    }

    private static void ThrowIfInvalid(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw new EdmException(EdmErrorCodes.ValidationFailed, errors);
        }
    }
}
