using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using WebApplication1.Dtos;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _db;

        public HomeController(ILogger<HomeController> logger, AppDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        public IActionResult Index()
        {
            if (AuthorizationHelper.IsAuthenticated(HttpContext))
            {
                var userId = AuthorizationHelper.GetUserId(HttpContext);
                var user = string.IsNullOrWhiteSpace(userId)
                    ? null
                    : _db.Users.AsNoTracking().FirstOrDefault(x => x.Id == userId);
                var route = LandingPageResolver.ResolveRoute(HttpContext, user);
                return route == LandingPageResolver.Appointments
                    ? RedirectToAction("Index", "Appointments")
                    : RedirectToAction("Index", "Cabinet");
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
