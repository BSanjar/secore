using WebApplication1.Modules.GenericModule.Models;

namespace WebApplication1.Modules.GenericModule.Services
{
    public interface ITableSource<T>
    {
        Task<PagedResult<T>> GetDataAsync(BaseFilterParams filter);
    }
}