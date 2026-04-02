using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Areas.Detsad.Controllers
{
    [Area("Detsad")]
    [RequireAuth]
    [Route("Detsad/GlobalSearch")]
    public class GlobalSearchController : Controller
    {
        private readonly AppDbContext _db;

        public GlobalSearchController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("Suggest")]
        public async Task<IActionResult> Suggest(string q)
        {
            var organizationId = AuthorizationHelper.GetOrganizationId(HttpContext);
            if (string.IsNullOrWhiteSpace(q) || string.IsNullOrEmpty(organizationId))
            {
                return Json(new { groups = Array.Empty<object>() });
            }

            q = q.Trim();
            var like = $"%{q}%";

            // Клиенты / дети
            var clients = await _db.OrganizationClients
                .Where(c =>
                    c.Organization == organizationId &&
                    (
                        (!string.IsNullOrEmpty(c.ClientName) && EF.Functions.ILike(c.ClientName, like)) ||
                        (!string.IsNullOrEmpty(c.Id) && EF.Functions.ILike(c.Id, like))
                    ))
                .OrderBy(c => c.ClientName)
                .Take(5)
                .Select(c => new
                {
                    title = c.ClientName ?? c.Id,
                    subtitle = "Клиент",
                    url = Url.Action("Children", "Cabinet", new { area = "Detsad", search = c.ClientName ?? c.Id })
                })
                .ToListAsync();

            // Счета
            var invoices = await _db.Invoices
                .Include(i => i.ClientNavigation)
                .Where(i =>
                    i.ClientNavigation != null &&
                    i.ClientNavigation.Organization == organizationId &&
                    (
                        (!string.IsNullOrEmpty(i.PayCode) && EF.Functions.ILike(i.PayCode, like)) ||
                        (!string.IsNullOrEmpty(i.NameInvoice) && EF.Functions.ILike(i.NameInvoice, like))
                    ))
                .OrderByDescending(i => i.DateCreated)
                .Take(5)
                .Select(i => new
                {
                    title = i.NameInvoice ?? i.PayCode ?? i.Id,
                    subtitle = $"Счёт {i.PayCode ?? i.Id}",
                    url = Url.Action("Details", "Invoices", new { area = "Detsad", id = i.Id })
                })
                .ToListAsync();

            var groups = new[]
            {
                new { title = "Клиенты", items = clients },
                new { title = "Счета", items = invoices }
            }
            .Where(g => g.items != null && g.items.Any())
            .ToList();

            return Json(new { groups });
        }
    }
}

