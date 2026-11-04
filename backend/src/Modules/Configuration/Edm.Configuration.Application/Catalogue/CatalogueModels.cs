using Edm.Configuration.Domain.Catalogue;

namespace Edm.Configuration.Application.Catalogue;

/// <summary>A category with the number of its subcategories that are not deleted (active or inactive).</summary>
public sealed record CategoryView(Category Category, int SubcategoryCount);

/// <summary>A subcategory with the name of its category, for display.</summary>
public sealed record SubcategoryView(Subcategory Subcategory, string CategoryName);

/// <summary>Input for creating a category. Values are as typed; the service validates them.</summary>
public sealed record CategoryCreate(string? Name, string? Description);

/// <summary>Input for updating a category. RowVersion is the version the user read (optimistic concurrency). A null IsActive keeps the current value.</summary>
public sealed record CategoryUpdate(string? Name, string? Description, bool? IsActive, long RowVersion);

/// <summary>Input for creating a subcategory under an active category.</summary>
public sealed record SubcategoryCreate(Guid? CategoryId, string? Name, string? Description);

/// <summary>Input for updating a subcategory. A subcategory cannot move to another category.</summary>
public sealed record SubcategoryUpdate(string? Name, string? Description, bool? IsActive, long RowVersion);

/// <summary>Audit snapshot of a category: the business fields only, so the diff shows real changes.</summary>
public sealed record CategorySnapshot(string Name, string? Description, bool IsActive);

/// <summary>Audit snapshot of a subcategory: the business fields only.</summary>
public sealed record SubcategorySnapshot(Guid CategoryId, string Name, string? Description, bool IsActive);
