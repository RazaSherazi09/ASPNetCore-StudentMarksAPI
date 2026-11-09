namespace MarksApi.Dtos.Common;

public class PagedResultDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public class ListQueryParameters
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;

    public int Page { get; set; } = DefaultPage;
    public int PageSize { get; set; } = DefaultPageSize;
    public string? Search { get; set; }
    public string? Sort { get; set; }

    public (int NormalizedPage, int NormalizedPageSize) Normalize()
    {
        var page = Page < 1 ? DefaultPage : Page;
        var pageSize = PageSize < 1 ? DefaultPageSize : (PageSize > MaxPageSize ? MaxPageSize : PageSize);
        return (page, pageSize);
    }
}

