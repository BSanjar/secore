using System.Collections.Generic;

namespace WebApplication1.Modules.GenericModule.Models
{
    public class PagedResult<T>
    {
        public List<T> Items { set; get; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
    }
}