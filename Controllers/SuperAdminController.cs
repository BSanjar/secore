using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.Controllers
{
    public class SuperAdminController : Controller
    {
        public IActionResult Index()
        {
            return  View();
        }
    }
}
