using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.DTOs.Common;

public class PaginationQuery
{
    [Range(1, int.MaxValue)]
    public int PageNumber { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 10;
}
