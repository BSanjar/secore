using System;
using System.Collections.Generic;

namespace WebApplication1.Dtos;

public class BaseFilterParams
{
    public string? SearchTerm { get; set; }
    public string? SearchColumn { get; set; }
    public int? ProviderId { get; set; }
    public int? RoleId { get; set; }
    public string? SortColumn { get; set; }
    public string SortDirection { get; set; } = "ASC";
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class PagedResult<T>
{
    public List<T> Items { set; get; } = new();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / Math.Max(1, PageSize));
}

public class GenericTableViewModel<T>
{
    public List<T> Items { get; set; } = new();
    public BaseFilterParams Filters { get; set; } = new();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalItems { get; set; }
    public string TableTitle { get; set; } = string.Empty;
    public string PartialRowViewName { get; set; } = string.Empty;
    public string PartialHeaderViewName { get; set; } = string.Empty;
    public string? ExportUrl { get; set; }
    public string LoadUrl { get; set; } = string.Empty;
    public Dictionary<string, string> SearchableColumns { get; set; } = new();
    public string? FilterPartialViewName { get; set; }
    public string? EmptyStateTitle { get; set; }
    public string? EmptyStateDescription { get; set; }
}
