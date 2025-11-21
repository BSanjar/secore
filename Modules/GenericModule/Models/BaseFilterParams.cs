namespace WebApplication1.Modules.GenericModule.Models
{
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
}