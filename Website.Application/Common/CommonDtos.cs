using System.ComponentModel.DataAnnotations;
using Website.Domain;

namespace Website.Application.Common;

/// <summary>Kết quả phân trang theo định dạng ABP: { totalCount, items }.</summary>
public class PagedResultDto<T>
{
    public int TotalCount { get; set; }
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    public PagedResultDto() { }

    public PagedResultDto(int totalCount, IReadOnlyList<T> items)
    {
        TotalCount = totalCount;
        Items = items;
    }
}

public class ListResultDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    public ListResultDto() { }

    public ListResultDto(IReadOnlyList<T> items) => Items = items;
}

/// <summary>Tham số phân trang theo định dạng ABP: skipCount / maxResultCount.</summary>
public class PagedInput
{
    [Range(0, int.MaxValue)]
    public int SkipCount { get; set; }

    [Range(1, AppConsts.MaxPageSize)]
    public int MaxResultCount { get; set; } = AppConsts.DefaultPageSize;
}

public class EntityDto<TKey>
{
    public TKey Id { get; set; } = default!;
}

public static class QueryableExtensions
{
    public static IQueryable<T> PageBy<T>(this IQueryable<T> query, PagedInput input) =>
        query.Skip(input.SkipCount).Take(input.MaxResultCount);
}
