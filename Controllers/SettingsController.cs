using Microsoft.AspNetCore.Mvc;
using WebApplication1.Helpers;

namespace WebApplication1.Controllers
{
    [RequireAuth]
    public class SettingsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}

