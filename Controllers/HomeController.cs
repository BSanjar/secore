using Microsoft.AspNetCore.Mvc;
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
            if (AuthorizationHelper.IsAuthenticated(HttpContext))
            {
                return RedirectToAction("Index", "Cabinet");
            }

            return RedirectToAction("Login", "Account");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

            if (HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            {
                var exceptionHandler = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                if (exceptionHandler?.Error != null)
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
