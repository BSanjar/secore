using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Helpers;
using WebApplication1.Models.DBModels;
using WebApplication1.Services;
using WebApplication1.Services.Cabinets;
using WebApplication1.ViewModels.Notifications;

namespace WebApplication1.Controllers;

[RequireAuth]
public class NotificationsController : Controller
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notificationService;
    private readonly NotificationRecipientResolver _recipientResolver;
    private readonly ICurrentTenantService _currentTenantService;

    public NotificationsController(
        AppDbContext db,
        NotificationService notificationService,
        NotificationRecipientResolver recipientResolver,
        ICurrentTenantService currentTenantService)
    {
        _db = db;
        _notificationService = notificationService;
        _recipientResolver = recipientResolver;
        _currentTenantService = currentTenantService;
    }

    private string? GetCurrentOrganizationId()
    {
        return AuthorizationHelper.GetOrganizationId(HttpContext);
    }

    private IActionResult? EnsureNotificationsFeature()
    {
        var tenant = _currentTenantService.GetCurrent();
        return tenant.Profile.HasFeature(CabinetFeatures.Notifications) ? null : NotFound();
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (!AuthorizationHelper.CanSendNotifications(HttpContext))
            return RedirectToAction("AccessDenied", "Home", new { permissionCode = "notifications.send" });

        var featureGuard = EnsureNotificationsFeature();
        if (featureGuard != null)
            return featureGuard;

        var organizationId = GetCurrentOrganizationId();
        if (string.IsNullOrEmpty(organizationId))
            return Unauthorized();

        var model = await BuildCreateViewModelAsync(new NotificationCreateViewModel(), organizationId, isMedclinic: IsMedclinicCabinet(), cancellationToken);
        return View(model);
    }

    private bool IsMedclinicCabinet() =>
        string.Equals(
            AuthorizationHelper.GetOrganizationType(HttpContext),
            "medclinic",
            StringComparison.OrdinalIgnoreCase);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NotificationCreateViewModel model, CancellationToken cancellationToken)
    {
        if (!AuthorizationHelper.CanSendNotifications(HttpContext))
            return RedirectToAction("AccessDenied", "Home", new { permissionCode = "notifications.send" });

        var featureGuard = EnsureNotificationsFeature();
        if (featureGuard != null)
            return featureGuard;

        var organizationId = GetCurrentOrganizationId();
        if (string.IsNullOrEmpty(organizationId))
            return Unauthorized();

        var isMedclinic = string.Equals(
            AuthorizationHelper.GetOrganizationType(HttpContext),
            "medclinic",
            StringComparison.OrdinalIgnoreCase);

        var selectedChannels = GetSelectedChannels(model);
        if (selectedChannels.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Выберите хотя бы один канал отправки.");
        }

        model.SelectedClientIds = (model.SelectedClientIds ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        model.SelectedGroupIds = (model.SelectedGroupIds ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        model.RecipientMode = string.IsNullOrWhiteSpace(model.RecipientMode)
            ? NotificationRecipientModes.All
            : model.RecipientMode.Trim();

        model.GenderFilter = model.GenderFilter?.Trim() ?? "";
        model.AgeFilter = model.AgeFilter?.Trim() ?? "";

        if (NotificationRecipientModes.IsManual(model.RecipientMode))
        {
            if (model.SelectedClientIds.Count == 0 &&
                (!isMedclinic && model.SelectedGroupIds.Count == 0))
            {
                ModelState.AddModelError(string.Empty, isMedclinic
                    ? "Выберите хотя бы одного пациента или используйте режим «Все пациенты»."
                    : "Выберите хотя бы одного клиента или одну группу.");
            }
        }

        if (!ModelState.IsValid)
        {
            await BuildCreateViewModelAsync(model, organizationId, isMedclinic, cancellationToken);
            return View(model);
        }

        var recipients = await _recipientResolver.ResolveAsync(
            organizationId,
            model.RecipientMode,
            model.GenderFilter,
            model.AgeFilter,
            model.SelectedClientIds,
            model.SelectedGroupIds,
            includeGroups: !isMedclinic,
            cancellationToken);

        if (recipients.Count == 0)
        {
            var hasDemographicFilters =
                !string.IsNullOrWhiteSpace(model.GenderFilter) ||
                !string.IsNullOrWhiteSpace(model.AgeFilter);

            ModelState.AddModelError(string.Empty, hasDemographicFilters
                ? "По выбранным фильтрам не найдено активных пациентов с заполненными полом и датой рождения."
                : NotificationRecipientModes.IsManual(model.RecipientMode)
                    ? "Не удалось найти активных пациентов среди выбранных."
                    : "В организации нет активных пациентов.");
            await BuildCreateViewModelAsync(model, organizationId, isMedclinic, cancellationToken);
            return View(model);
        }

        var result = await _notificationService.CreateManualNotificationsAsync(
            recipients,
            selectedChannels,
            model.Subject.Trim(),
            model.Message.Trim(),
            AuthorizationHelper.GetUserId(HttpContext),
            cancellationToken);

        if (result.CreatedNotifications == 0)
        {
            ModelState.AddModelError(string.Empty, "Для выбранных клиентов не найдено контактов по указанным каналам.");
            await BuildCreateViewModelAsync(model, organizationId, isMedclinic, cancellationToken);
            return View(model);
        }

        TempData["Message"] =
            $"Рассылка поставлена в очередь. Клиентов: {result.SelectedClients}, в очереди: {result.ClientsQueued}, " +
            $"создано уведомлений: {result.CreatedNotifications}.";

        if (result.ClientsSkippedWithoutChannel > 0)
        {
            TempData["Warning"] =
                $"Пропущено клиентов без подходящих контактов: {result.ClientsSkippedWithoutChannel}.";
        }

        return RedirectToAction(nameof(Index));
    }

    [RequirePermission("notifications.view")]
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
        var featureGuard = EnsureNotificationsFeature();
        if (featureGuard != null)
            return featureGuard;

        var organizationId = GetCurrentOrganizationId();
        if (string.IsNullOrEmpty(organizationId))
            return Unauthorized();

        // По умолчанию — только за сегодня (если даты явно не переданы в query).
        var hasCreatedFrom = Request.Query.ContainsKey("createdFrom");
        var hasCreatedTo = Request.Query.ContainsKey("createdTo");
        if (!hasCreatedFrom && !hasCreatedTo)
        {
            var today = DateTime.Today;
            createdFrom = today.ToString("yyyy-MM-dd");
            createdTo = today.ToString("yyyy-MM-dd");
        }

        var query = _db.Notifications
            .AsNoTracking()
            .Include(n => n.ClientNavigation)
            .Where(n => n.ClientNavigation != null && n.ClientNavigation.Organization == organizationId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(n => n.Status == status);
        if (!string.IsNullOrWhiteSpace(channel))
            query = query.Where(n => n.Channel == channel);
        if (DateTime.TryParse(createdFrom, out var cf))
            query = query.Where(n => n.CreatedAt != null && n.CreatedAt.Value.Date >= cf.Date);
        if (DateTime.TryParse(createdTo, out var ct))
            query = query.Where(n => n.CreatedAt != null && n.CreatedAt.Value.Date <= ct.Date);
        if (DateTime.TryParse(sentFrom, out var sf))
            query = query.Where(n => n.SentAt != null && n.SentAt.Value.Date >= sf.Date);
        if (DateTime.TryParse(sentTo, out var st))
            query = query.Where(n => n.SentAt != null && n.SentAt.Value.Date <= st.Date);

        query = query.OrderByDescending(n => n.CreatedAt);

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var totalCount = await query.CountAsync();
        var list = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = totalCount > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 1;
        ViewBag.Status = status ?? "";
        ViewBag.Channel = channel ?? "";
        ViewBag.CreatedFrom = createdFrom ?? "";
        ViewBag.CreatedTo = createdTo ?? "";
        ViewBag.SentFrom = sentFrom ?? "";
        ViewBag.SentTo = sentTo ?? "";
        ViewBag.CanSendNotifications = AuthorizationHelper.CanSendNotifications(HttpContext);

        return View(list);
    }

    private async Task<NotificationCreateViewModel> BuildCreateViewModelAsync(
        NotificationCreateViewModel model,
        string organizationId,
        bool isMedclinic,
        CancellationToken cancellationToken)
    {
        var clients = await _db.OrganizationClients
            .AsNoTracking()
            .Where(c => c.Organization == organizationId && c.ClientStatus == 1)
            .OrderBy(c => c.ClientName)
            .Select(c => new NotificationRecipientOptionViewModel
            {
                Id = c.Id,
                Name = c.ClientName ?? c.Id,
                GroupId = c.OrgClientGroupId,
                GroupName = c.OrgClientGroup != null ? c.OrgClientGroup.Name : null,
                StatusLabel = c.ClientStatus == 1 ? "Активный" : "Неактивный",
                Email = c.ClientEmail,
                Telegram = c.ClientTg,
                WhatsApp = c.ClientWa
            })
            .ToListAsync(cancellationToken);

        var demographics = await _recipientResolver.LoadDemographicsAsync(
            organizationId,
            clients.Select(c => c.Id),
            cancellationToken);

        foreach (var client in clients)
        {
            if (!demographics.TryGetValue(client.Id, out var demo))
                continue;

            client.Gender = demo.Gender;
            client.Age = demo.Age;
        }

        model.AvailableClients = clients;

        if (!isMedclinic)
        {
            model.AvailableGroups = await _db.OrgClientGroups
                .AsNoTracking()
                .Where(g => g.OrganizationId == organizationId && g.IsDeleted == 0)
                .OrderBy(g => g.Name)
                .Select(g => new NotificationGroupOptionViewModel
                {
                    Id = g.Id,
                    Name = g.Name ?? g.Id,
                    ClientCount = g.OrganizationClients.Count(c => c.ClientStatus == 1)
                })
                .ToListAsync(cancellationToken);
        }
        else
        {
            model.AvailableGroups = Array.Empty<NotificationGroupOptionViewModel>();
            model.SelectedGroupIds.Clear();
        }

        return model;
    }

    private static List<string> GetSelectedChannels(NotificationCreateViewModel model)
    {
        var channels = new List<string>();

        if (model.SendEmail)
            channels.Add("email");
        if (model.SendTelegram)
            channels.Add("telegram");
        if (model.SendWhatsApp)
            channels.Add("whatsapp");

        return channels;
    }
}
