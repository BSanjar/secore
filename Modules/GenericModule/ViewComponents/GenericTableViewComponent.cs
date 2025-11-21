using Microsoft.AspNetCore.Mvc;

using WebApplication1.Modules.GenericModule.Services;
using WebApplication1.Modules.GenericModule.Models;

namespace WebApplication1.Modules.GenericModule.ViewComponents
{
    public class GenericTableViewComponent : ViewComponent
    {
        //public async Task<IViewComponentResult> InvokeAsync<T>(
        //    ITableSource<T> source,
        //    BaseFilterParams filters,
        //    string tableTitle,
        //    string partialRowViewName,
        //    string partialHeaderViewName,
        //    string exportActionUrl,
        //    string loadActionUrl,
        //    Dictionary<string, string> searchableColumns)
        //{
        //    var result = await source.GetDataAsync(filters);

        //    var viewModel = new GenericTableViewModel<T>
        //    {
        //        Items = result.Items,
        //        PageNumber = result.PageNumber,
        //        PageSize = result.PageSize,
        //        TotalPages = result.TotalPages,
        //        TotalItems = result.TotalItems,
        //        Filters = filters,
        //        TableTitle = tableTitle,
        //        PartialRowViewName = partialRowViewName,
        //        PartialHeaderViewName = partialHeaderViewName,
        //        ExportUrl = exportActionUrl,
        //        LoadUrl = loadActionUrl,
        //        SearchableColumns = searchableColumns
        //    };

        //    return View(viewModel);
        //}

        public async Task<IViewComponentResult> InvokeAsync<T>(
            ITableSource<T> source,
            BaseFilterParams filters,
            string tableTitle,
            string partialRowViewName,
            string partialHeaderViewName,
            string? exportActionUrl,
            string loadActionUrl,
            Dictionary<string, string> searchableColumns,
            string? filterPartialViewName = null,
            string? emptyStateTitle = null,
            string? emptyStateDescription = null)
        {
            var result = await source.GetDataAsync(filters);

            var viewModel = new GenericTableViewModel<dynamic>
            {
                Items = result.Items.Cast<dynamic>().ToList(),
                PageNumber = result.PageNumber,
                PageSize = result.PageSize,
                TotalItems = result.TotalItems,
                TotalPages = result.TotalPages,
                Filters = filters,
                TableTitle = tableTitle,
                PartialRowViewName = partialRowViewName,
                PartialHeaderViewName = partialHeaderViewName,
                ExportUrl = exportActionUrl,
                LoadUrl = loadActionUrl,
                SearchableColumns = searchableColumns,
                FilterPartialViewName = filterPartialViewName,
                EmptyStateTitle = emptyStateTitle,
                EmptyStateDescription = emptyStateDescription
            };

            return View(viewModel);
        }

    }
}
