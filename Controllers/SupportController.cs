using Microsoft.AspNetCore.Mvc;
using WebApplication1.Helpers;

namespace WebApplication1.Controllers
{
    [RequireAuth]
    public class SupportController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(string? Topic, string? Message, string? ContactEmail, string? ContactPhone)
        {
            if (string.IsNullOrWhiteSpace(Message))
            {
                ViewBag.Topic = Topic;
                ViewBag.Message = Message;
                ViewBag.ContactEmail = ContactEmail;
                ViewBag.ContactPhone = ContactPhone;
                ModelState.AddModelError("Message", "Введите сообщение.");
                return View();
            }

            TempData["Message"] = "Ваше обращение принято. Мы ответим в ближайшее время.";
            return RedirectToAction(nameof(Index));
        }
    }
}
