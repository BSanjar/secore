using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebApplication1.Helpers;

namespace WebApplication1.Controllers
{
    public class BaseController : Controller
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Проверяем авторизацию для всех действий, кроме Login и Logout 
            var controllerName = context.RouteData.Values["controller"]?.ToString();
            var actionName = context.RouteData.Values["action"]?.ToString();

            if (controllerName != "Account" && controllerName != "Home")
            {
                if (!AuthorizationHelper.IsAuthenticated(context.HttpContext))
                {
                    context.Result = RedirectToAction("Login", "Account", new { returnUrl = context.HttpContext.Request.Path });
                    return;
                }
            }

            base.OnActionExecuting(context);
        }
    }
}

