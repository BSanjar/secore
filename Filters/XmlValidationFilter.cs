using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebApplication1.Models.WebApiModels;
using WebApplication1.Services;

namespace WebApplication1.Filters
{
    /// <summary>
    /// Фильтр для обработки ошибок валидации и десериализации XML запросов
    /// </summary>
    public class XmlValidationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            // Проверяем, что это XML запрос
            var contentType = context.HttpContext.Request.ContentType;
            if (contentType != null && (contentType.Contains("application/xml") || contentType.Contains("text/xml")))
            {
                // Обрабатываем только ошибки десериализации (когда request == null)
                // Валидация полей выполняется внутри контроллера
                if (context.ActionArguments.Values.Any(v => v == null))
                {
                    var actionName = context.RouteData.Values["action"]?.ToString()?.ToLower();

                    object? errorResponse = actionName switch
                    {
                        "check" => WebApiResponseService.CreateCheckErrorResponse(ErrorCode.UnknownRequest),
                        "pay" => WebApiResponseService.CreatePayErrorResponse(ErrorCode.UnknownRequest),
                        "payinfo" => WebApiResponseService.CreatePayInfoErrorResponse(ErrorCode.UnknownRequest),
                        _ => null
                    };

                    if (errorResponse != null)
                    {
                        context.Result = new ObjectResult(errorResponse)
                        {
                            StatusCode = 200 // Возвращаем 200, так как это валидный XML ответ с кодом ошибки
                        };
                        context.HttpContext.Response.ContentType = "application/xml";
                    }
                }
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // Не требуется обработка после выполнения действия
        }
    }
}

