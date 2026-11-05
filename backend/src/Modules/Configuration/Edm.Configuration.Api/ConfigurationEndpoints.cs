using Edm.Configuration.Application.Catalogue;
using Edm.Web.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Edm.Configuration.Api;

/// <summary>Request body for creating a category.</summary>
public sealed record CategoryCreateRequest(string? Name, string? Description);

/// <summary>Request body for updating a category. A missing IsActive keeps the current value.</summary>
public sealed record CategoryUpdateRequest(string? Name, string? Description, bool? IsActive, long RowVersion);

/// <summary>Request body for creating a subcategory.</summary>
public sealed record SubcategoryCreateRequest(Guid? CategoryId, string? Name, string? Description);

/// <summary>Request body for updating a subcategory. A missing IsActive keeps the current value.</summary>
public sealed record SubcategoryUpdateRequest(string? Name, string? Description, bool? IsActive, long RowVersion);

/// <summary>Category as returned to the client. RowVersion must be sent back on every update or delete.</summary>
public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    long RowVersion,
    int SubcategoryCount,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy)
{
    public static CategoryResponse From(CategoryView view)
        => new(
            view.Category.Id,
            view.Category.Name,
            view.Category.Description,
            view.Category.IsActive,
            view.Category.RowVersion,
            view.SubcategoryCount,
            view.Category.UpdatedAtUtc,
            view.Category.UpdatedBy);
}

/// <summary>Subcategory as returned to the client.</summary>
public sealed record SubcategoryResponse(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? Description,
    bool IsActive,
    long RowVersion,
    DateTimeOffset UpdatedAtUtc,
    Guid? UpdatedBy)
{
    public static SubcategoryResponse From(SubcategoryView view)
        => new(
            view.Subcategory.Id,
            view.Subcategory.CategoryId,
            view.CategoryName,
            view.Subcategory.Name,
            view.Subcategory.Description,
            view.Subcategory.IsActive,
            view.Subcategory.RowVersion,
            view.Subcategory.UpdatedAtUtc,
            view.Subcategory.UpdatedBy);
}

/// <summary>
/// The configuration endpoints (Master Prompt Part 21.2). Each endpoint declares exactly one permission (ADR-0007).
/// Categories: config.category.view to read, config.category.manage to change. Subcategories have no view-only
/// permission in the catalogue, so every subcategory endpoint uses config.subcategory.manage.
/// </summary>
public static class ConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Configuration");

        group.MapGet("/categories", async Task<IResult> (CategoryService service, bool? includeInactive, CancellationToken cancellationToken) =>
            {
                var items = await service.ListAsync(includeInactive ?? false, cancellationToken);
                return Results.Ok(items.Select(CategoryResponse.From).ToArray());
            })
            .RequiresPermission("config.category.view")
            .WithName("ListCategories")
            .WithSummary("List categories. Inactive categories are included only when asked.");

        group.MapPost("/categories", async Task<IResult> (CategoryCreateRequest body, CategoryService service, CancellationToken cancellationToken) =>
            {
                var created = await service.CreateAsync(new CategoryCreate(body.Name, body.Description), cancellationToken);
                var response = CategoryResponse.From(created);
                return Results.Created($"/api/v1/categories/{response.Id}", response);
            })
            .RequiresPermission("config.category.manage")
            .WithName("CreateCategory")
            .WithSummary("Create a category.");

        group.MapPut("/categories/{id:guid}", async Task<IResult> (Guid id, CategoryUpdateRequest body, CategoryService service, CancellationToken cancellationToken) =>
            {
                var updated = await service.UpdateAsync(
                    id,
                    new CategoryUpdate(body.Name, body.Description, body.IsActive, body.RowVersion),
                    cancellationToken);
                return updated is null ? Results.NotFound() : Results.Ok(CategoryResponse.From(updated));
            })
            .RequiresPermission("config.category.manage")
            .WithName("UpdateCategory")
            .WithSummary("Update a category, or deactivate or reactivate it. Needs the current row version.");

        group.MapDelete("/categories/{id:guid}", async Task<IResult> (Guid id, [FromQuery] long rowVersion, CategoryService service, CancellationToken cancellationToken) =>
            {
                var deleted = await service.DeleteAsync(id, rowVersion, cancellationToken);
                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .RequiresPermission("config.category.manage")
            .WithName("DeleteCategory")
            .WithSummary("Delete a category. Refused while it has subcategories or is used.");

        group.MapGet("/subcategories", async Task<IResult> (Guid? categoryId, bool? includeInactive, SubcategoryService service, CancellationToken cancellationToken) =>
            {
                var items = await service.ListAsync(categoryId, includeInactive ?? false, cancellationToken);
                return Results.Ok(items.Select(SubcategoryResponse.From).ToArray());
            })
            .RequiresPermission("config.subcategory.manage")
            .WithName("ListSubcategories")
            .WithSummary("List subcategories, optionally for one category.");

        group.MapPost("/subcategories", async Task<IResult> (SubcategoryCreateRequest body, SubcategoryService service, CancellationToken cancellationToken) =>
            {
                var created = await service.CreateAsync(new SubcategoryCreate(body.CategoryId, body.Name, body.Description), cancellationToken);
                var response = SubcategoryResponse.From(created);
                return Results.Created($"/api/v1/subcategories/{response.Id}", response);
            })
            .RequiresPermission("config.subcategory.manage")
            .WithName("CreateSubcategory")
            .WithSummary("Create a subcategory under an active category.");

        group.MapPut("/subcategories/{id:guid}", async Task<IResult> (Guid id, SubcategoryUpdateRequest body, SubcategoryService service, CancellationToken cancellationToken) =>
            {
                var updated = await service.UpdateAsync(
                    id,
                    new SubcategoryUpdate(body.Name, body.Description, body.IsActive, body.RowVersion),
                    cancellationToken);
                return updated is null ? Results.NotFound() : Results.Ok(SubcategoryResponse.From(updated));
            })
            .RequiresPermission("config.subcategory.manage")
            .WithName("UpdateSubcategory")
            .WithSummary("Update a subcategory, or deactivate or reactivate it. Needs the current row version.");

        group.MapDelete("/subcategories/{id:guid}", async Task<IResult> (Guid id, [FromQuery] long rowVersion, SubcategoryService service, CancellationToken cancellationToken) =>
            {
                var deleted = await service.DeleteAsync(id, rowVersion, cancellationToken);
                return deleted ? Results.NoContent() : Results.NotFound();
            })
            .RequiresPermission("config.subcategory.manage")
            .WithName("DeleteSubcategory")
            .WithSummary("Delete a subcategory. Refused while it is used.");

        group.MapDeptHeadLimitEndpoints();
        group.MapCurrencyEndpoints();
        group.MapApprovalAuthorityEndpoints();
        return app;
    }
}
