using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace WebApplication1.Controllers
{
    [IgnoreAntiforgeryToken]
    public class LanguageController : Controller
    {
        [HttpPost]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            if (string.IsNullOrEmpty(culture))
            {
                culture = "ru";
            }

            // Валидация культуры
            var supportedCultures = new[] { "ru", "en", "ky" };
            if (!supportedCultures.Contains(culture))
            {
                culture = "ru";
            }

            // Создаем RequestCulture с явным указанием культуры
            var requestCulture = new RequestCulture(culture, culture);
            
            // Устанавливаем культуру для текущего потока (важно для IHtmlLocalizer)
            Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
            
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(requestCulture),
                new CookieOptions 
                { 
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = false, // Должен быть доступен для JavaScript
                    SameSite = SameSiteMode.Lax,
                    Path = "/" // Cookie должен быть доступен для всего сайта
                }
            );

            // Если returnUrl пустой или не является локальным, перенаправляем на главную
            if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
            {
                return RedirectToAction("Index", "Home");
            }

            return LocalRedirect(returnUrl);
        }
    }
}

