using SmartGarage.Domain.Constants;

namespace SmartGarage.Application.Common;

/// <summary>
/// Paging, sorting and free-text search shared by every list endpoint. The clamped values are
/// the only ones services read, so a client cannot ask for an unbounded page.
/// </summary>
public abstract class BaseSearchRequest
{
    public int Page { get; init; } = PagingLimits.DefaultPage;

    public int PageSize { get; init; } = PagingLimits.DefaultPageSize;

    public string? Search { get; init; }

    public string? OrderBy { get; init; }

    public bool Descending { get; init; }

    public int ClampedPage => PagingLimits.ClampPage(Page);

    public int ClampedPageSize => PagingLimits.ClampPageSize(PageSize);

    public int Skip => (ClampedPage - 1) * ClampedPageSize;
}
