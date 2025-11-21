using System.Collections.Generic;

namespace WebApplication1.Modules.GenericModule.Models
{
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
}