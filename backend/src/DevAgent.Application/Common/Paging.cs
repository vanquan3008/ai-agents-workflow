namespace DevAgent.Application.Common;

/// <summary>Phân trang dạng page/pageSize. pageSize bị kẹp trong 1..100 để chặn truy vấn quá lớn.</summary>
public sealed record PageRequest
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    public PageRequest(int? page, int? pageSize)
    {
        Page = Math.Max(1, page ?? 1);
        PageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
    }

    public int Page { get; }

    public int PageSize { get; }

    public int Skip => (Page - 1) * PageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasNextPage => Page < TotalPages;
}
