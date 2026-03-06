using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Controllers;

[RequireAuth]
public class NotificationsController : Controller
{
    private readonly AppDbContext _db;

    public NotificationsController(AppDbContext db)
    {
        _db = db;
    }

    private string? GetCurrentOrganizationId()
    {
        return AuthorizationHelper.GetOrganizationId(HttpContext);
    }

    public async Task<IActionResult> Index(
        int page = 1,
        int pageSize = 20,
        string? status = null,
        string? channel = null,
        string? createdFrom = null,
        string? createdTo = null,
        string? sentFrom = null,
        string? sentTo = null)
    {
        var organizationId = GetCurrentOrganizationId();
        if (string.IsNullOrEmpty(organizationId))
            return Unauthorized();

        var query = _db.Notifications
            .Include(n => n.ClientNavigation)
            .Where(n => n.ClientNavigation != null && n.ClientNavigation.Organization == organizationId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(n => n.Status == status);
        if (!string.IsNullOrWhiteSpace(channel))
            query = query.Where(n => n.Channel == channel);
        if (DateTime.TryParse(createdFrom, out var cf))
            query = query.Where(n => n.CreatedAt != null && n.CreatedAt.Value.Date >= cf.Date);
        if (DateTime.TryParse(createdTo, out var ct))
            query = query.Where(n => n.CreatedAt != null && n.CreatedAt.Value.Date <= ct.Date.AddDays(1));
        if (DateTime.TryParse(sentFrom, out var sf))
            query = query.Where(n => n.SentAt != null && n.SentAt.Value.Date >= sf.Date);
        if (DateTime.TryParse(sentTo, out var st))
            query = query.Where(n => n.SentAt != null && n.SentAt.Value.Date <= st.Date.AddDays(1));

        query = query.OrderByDescending(n => n.CreatedAt);

        var totalCount = await query.CountAsync();
        var list = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = totalCount > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 1;
        ViewBag.Status = status;
        ViewBag.Channel = channel;
        ViewBag.CreatedFrom = createdFrom;
        ViewBag.CreatedTo = createdTo;
        ViewBag.SentFrom = sentFrom;
        ViewBag.SentTo = sentTo;

        return View(list);
    }
}
