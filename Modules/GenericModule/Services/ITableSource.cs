using WebApplication1.Dtos;

namespace WebApplication1.Modules.GenericModule.Services
{
    public interface ITableSource<T>
    {
        Task<PagedResult<T>> GetDataAsync(BaseFilterParams filter);
    }
}