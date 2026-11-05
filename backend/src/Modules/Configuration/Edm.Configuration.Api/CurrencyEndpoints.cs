using System.Globalization;
using Edm.Configuration.Application.Currencies;
using Edm.Configuration.Domain.Currencies;
using Edm.Web.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Edm.Configuration.Api;

public sealed record CurrencyCreateRequest(string? Code, string? Name, string? Rate);

public sealed record CurrencyUpdateRequest(string? Name, long RowVersion);

public sealed record CurrencyRateChangeRequest(string? Rate, long RowVersion);

public sealed record CurrencyStateRequest(long RowVersion);

public sealed record CurrencyResponse(
    Guid Id,
    string Code,
    string Name,
    bool IsBase,
    bool IsActive,
    string? Rate,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy)
{
    public static CurrencyResponse From(CurrencyView view)
        => new(
            view.Currency.Id,
            view.Currency.Code,
            view.Currency.Name,
            view.Currency.IsBase,
            view.Currency.IsActive,
            view.ActiveRate?.Rate.ToString("0.####", CultureInfo.InvariantCulture),
            view.Currency.RowVersion,
            view.Currency.UpdatedAtUtc,
            view.Currency.UpdatedBy);
}

public sealed record CurrencyRateResponse(
    Guid Id,
    string Rate,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    bool IsActive,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy)
{
    public static CurrencyRateResponse From(CurrencyRate rate)
        => new(
            rate.Id,
            rate.Rate.ToString("0.####", CultureInfo.InvariantCulture),
            rate.EffectiveFrom,
            rate.EffectiveTo,
            rate.IsActive,
            rate.RowVersion,
            rate.UpdatedAtUtc,
            rate.UpdatedBy);
}

/// <summary>Currency master and rates (Master Prompt Part 5.6); permission is config.currency.manage.</summary>
public static class CurrencyEndpoints
{
    public static IEndpointRouteBuilder MapCurrencyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/currencies").WithTags("Configuration");

        group.MapGet("", async Task<IResult> (
                CurrencyService service,
                bool? includeInactive,
                CancellationToken cancellationToken) =>
            {
                var items = await service.ListAsync(includeInactive ?? false, cancellationToken);
                return Results.Ok(items.Select(CurrencyResponse.From).ToArray());
            })
            .RequiresPermission("config.currency.manage")
            .WithName("ListCurrencies")
            .WithSummary("List currencies with their current PKR conversion rates.");

        group.MapPost("", async Task<IResult> (
                CurrencyCreateRequest body,
                CurrencyService service,
                CancellationToken cancellationToken) =>
            {
                var created = await service.CreateAsync(
                    new CurrencyCreate(body.Code, body.Name, body.Rate),
                    cancellationToken);
                var response = CurrencyResponse.From(created);
                return Results.Created($"/api/v1/currencies/{response.Id}", response);
            })
            .RequiresPermission("config.currency.manage")
            .WithName("CreateCurrency")
            .WithSummary("Create a currency and its initial PKR conversion rate.");

        group.MapPut("/{id:guid}", async Task<IResult> (
                Guid id,
                CurrencyUpdateRequest body,
                CurrencyService service,
                CancellationToken cancellationToken) =>
            {
                var updated = await service.UpdateAsync(
                    id,
                    new CurrencyUpdate(body.Name, body.RowVersion),
                    cancellationToken);
                return updated is null ? Results.NotFound() : Results.Ok(CurrencyResponse.From(updated));
            })
            .RequiresPermission("config.currency.manage")
            .WithName("UpdateCurrency")
            .WithSummary("Update a currency name. Currency codes cannot be changed.");

        group.MapPost("/{id:guid}/rates", async Task<IResult> (
                Guid id,
                CurrencyRateChangeRequest body,
                CurrencyService service,
                CancellationToken cancellationToken) =>
            {
                var rate = await service.SetRateAsync(
                    id,
                    new CurrencyRateChange(body.Rate, body.RowVersion),
                    cancellationToken);
                return rate is null ? Results.NotFound() : Results.Created(
                    $"/api/v1/currencies/{id}/rates/{rate.Id}",
                    CurrencyRateResponse.From(rate));
            })
            .RequiresPermission("config.currency.manage")
            .WithName("CreateCurrencyRate")
            .WithSummary("Append a new effective-dated rate, preserving prior rates.");

        group.MapGet("/{id:guid}/rates", async Task<IResult> (
                Guid id,
                CurrencyService service,
                CancellationToken cancellationToken) =>
            {
                var rates = await service.ListRatesAsync(id, cancellationToken);
                return rates is null
                    ? Results.NotFound()
                    : Results.Ok(rates.Select(CurrencyRateResponse.From).ToArray());
            })
            .RequiresPermission("config.currency.manage")
            .WithName("ListCurrencyRates")
            .WithSummary("List a currency's rate history.");

        group.MapPost("/{id:guid}/activate", async Task<IResult> (
                Guid id,
                CurrencyStateRequest body,
                CurrencyService service,
                CancellationToken cancellationToken) =>
            {
                var updated = await service.SetActiveAsync(id, true, body.RowVersion, cancellationToken);
                return updated is null ? Results.NotFound() : Results.Ok(CurrencyResponse.From(updated));
            })
            .RequiresPermission("config.currency.manage")
            .WithName("ActivateCurrency")
            .WithSummary("Activate a currency.");

        group.MapPost("/{id:guid}/deactivate", async Task<IResult> (
                Guid id,
                CurrencyStateRequest body,
                CurrencyService service,
                CancellationToken cancellationToken) =>
            {
                var updated = await service.SetActiveAsync(id, false, body.RowVersion, cancellationToken);
                return updated is null ? Results.NotFound() : Results.Ok(CurrencyResponse.From(updated));
            })
            .RequiresPermission("config.currency.manage")
            .WithName("DeactivateCurrency")
            .WithSummary("Deactivate a currency. PKR cannot be deactivated.");

        group.MapDelete("/{id:guid}", async Task<IResult> (
                Guid id,
                [FromQuery] long rowVersion,
                CurrencyService service,
                CancellationToken cancellationToken) =>
            {
                var deleted = await service.DeleteAsync(id, rowVersion, cancellationToken);
                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .RequiresPermission("config.currency.manage")
            .WithName("DeleteCurrency")
            .WithSummary("Delete a currency only when it has no rates or other usage.");

        return app;
    }
}
