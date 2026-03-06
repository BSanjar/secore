using Microsoft.AspNetCore.Mvc.Filters;

namespace WebApplication1.Filters
{
    
    // При заходе в кабинет (area) сохраняет имя area в сессии.
    // Корневой _ViewStart использует это значение для выбора layout при открытии страниц Settings, Support, Instructions ...
    
    public class SetCabinetLayoutFilter : IActionFilter
    {
        private static readonly string[] CabinetAreas = { "Detsad", "Medclinic", "Standart", "Simple" };

        public void OnActionExecuting(ActionExecutingContext context)
        {
            var area = context.RouteData.Values["area"]?.ToString();
            if (!string.IsNullOrEmpty(area) && CabinetAreas.Contains(area))
                context.HttpContext.Session.SetString("CabinetLayout", area);
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
