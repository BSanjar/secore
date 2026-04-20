using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace WebApplication1.Services;

/// <summary>
/// Renders a Razor view to string for use in PDF generation etc.
/// </summary>
public class ViewRenderService
{
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IServiceProvider _serviceProvider;

    public ViewRenderService(
        IRazorViewEngine viewEngine,
        ITempDataProvider tempDataProvider,
        IServiceProvider serviceProvider)
    {
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> RenderViewToStringAsync<TModel>(ControllerContext controllerContext, string viewName, TModel model, ViewDataDictionary? viewData = null)
    {
        var viewDataDict = viewData ?? new ViewDataDictionary<TModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
        var tempDataDict = new TempDataDictionary(controllerContext.HttpContext, _tempDataProvider);

        var viewResult = _viewEngine.FindView(controllerContext, viewName, true);
        if (viewResult.View == null)
        {
            var searched = string.Join(", ", viewResult.SearchedLocations);
            throw new InvalidOperationException($"View '{viewName}' not found. Searched: {searched}");
        }

        await using var sw = new StringWriter();
        var viewContext = new ViewContext(
            controllerContext,
            viewResult.View,
            viewDataDict,
            tempDataDict,
            sw,
            new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        return sw.ToString();
    }
}
