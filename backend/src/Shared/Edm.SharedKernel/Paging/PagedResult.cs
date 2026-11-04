namespace Edm.SharedKernel.Paging;

/// <summary>A page of results with the total count (Part 21.1: items, total count and page metadata).</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
