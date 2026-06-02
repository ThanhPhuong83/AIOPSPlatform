namespace HrmAiOps.Application.Abstractions;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}

public static class PaginationExtensions
{
    public static PagedResult<T> ToPagedResult<T>(this IEnumerable<T> source, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var all = source.ToList();
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<T>(items, page, pageSize, all.Count);
    }
}
