using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;
using System.Diagnostics;
using WebApplication1.Dtos;
using WebApplication1.Helpers;

namespace WebApplication1.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            // Если пользователь авторизован, перенаправляем в его личный кабинет
            if (AuthorizationHelper.IsAuthenticated(HttpContext))
            {
                var organizationType = AuthorizationHelper.GetOrganizationType(HttpContext);
                var area = GetAreaByOrganizationType(organizationType);
                return RedirectToAction("Index", "Cabinet", new { area = area });
            }

            // Если не авторизован, перенаправляем на страницу входа
            return RedirectToAction("Login", "Account");
        }

        private string GetAreaByOrganizationType(string? organizationType)
        {
            return organizationType?.ToLower() switch
            {
                "detsad" => "Detsad",
                "standart" => "Standart",
                "school" => "School",
                "medclinic" => "Medclinic",
                "simple" => "Simple",
                _ => "Standart" // По умолчанию
            };
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            
            // В режиме разработки логируем ошибку
            if (HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            {
                var exceptionHandler = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                if (exceptionHandler != null && exceptionHandler.Error != null)
                {
                    _logger.LogError(exceptionHandler.Error, "Error occurred. Request ID: {RequestId}", requestId);
                }
            }
            
            return View(new ErrorViewModel { RequestId = requestId });
        }

        public IActionResult AccessDenied(string? permissionCode = null)
        {
            ViewBag.PermissionCode = permissionCode;
            return View();
        }
    }
}