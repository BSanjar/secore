using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Helpers
{
    /// <summary>
    /// Атрибут для проверки авторизации пользователя и активности организации (Organization.IsActive).
    /// </summary>
    public class RequireAuthAttribute : ActionFilterAttribute
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var organizationId = context.HttpContext.Session.GetString("OrganizationId");
            var userId = context.HttpContext.Session.GetString("UserId");
            var organizationType = context.HttpContext.Session.GetString("OrganizationType");

            if (string.IsNullOrEmpty(organizationId) || string.IsNullOrEmpty(userId))
            {
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl = context.HttpContext.Request.Path });
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            if (!await SubscriptionHelper.HasSubscriptionAccessAsync(db, organizationId))
            {
                context.Result = new RedirectToActionResult("SubscriptionExpired", "Account", new { area = "" });
                return;
            }

            if (string.Equals(organizationType, "medclinic", StringComparison.OrdinalIgnoreCase)
                && AuthorizationHelper.IsDoctorOnlyMode(context.HttpContext))
            {
                var controller = context.RouteData.Values["controller"]?.ToString();
                var allowedWithoutPermission = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Account",
                    "Language",
                    "Home"
                };

                if (!string.IsNullOrWhiteSpace(controller) && !allowedWithoutPermission.Contains(controller))
                {
                    var hasControllerPermission = controller.Equals("Appointments", StringComparison.OrdinalIgnoreCase)
                        ? PermissionHelper.HasPermission(context.HttpContext, "appointments.doctor.view")
                          || PermissionHelper.HasPermission(context.HttpContext, "appointments.registry.view")
                          || PermissionHelper.HasPermission(context.HttpContext, "appointments.view")
                        : controller.Equals("Invoices", StringComparison.OrdinalIgnoreCase)
                            ? PermissionHelper.HasPermission(context.HttpContext, "invoices.view")
                            : controller.Equals("Payments", StringComparison.OrdinalIgnoreCase)
                                ? PermissionHelper.HasPermission(context.HttpContext, "transactions.view")
                                : controller.Equals("Doctors", StringComparison.OrdinalIgnoreCase)
                                    || controller.Equals("Users", StringComparison.OrdinalIgnoreCase)
                                    ? PermissionHelper.HasPermission(context.HttpContext, "doctors.view")
                                    : false;

                    if (!hasControllerPermission)
                    {
                        context.Result = new RedirectToActionResult("AccessDenied", "Home", new { permissionCode = "restricted.controller" });
                        return;
                    }
                }
            }

            await next();
        }
    }

    /// <summary>
    /// Атрибут для проверки прав доступа пользователя
    /// </summary>
    public class RequirePermissionAttribute : ActionFilterAttribute
    {
        private readonly string _permissionCode;

        public RequirePermissionAttribute(string permissionCode)
        {
            _permissionCode = permissionCode;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var organizationId = context.HttpContext.Session.GetString("OrganizationId");
            var userId = context.HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(organizationId) || string.IsNullOrEmpty(userId))
            {
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl = context.HttpContext.Request.Path });
                return;
            }

            var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            if (!await SubscriptionHelper.HasSubscriptionAccessAsync(dbContext, organizationId))
            {
                context.Result = new RedirectToActionResult("SubscriptionExpired", "Account", new { area = "" });
                return;
            }

            var hasPermission = PermissionHelper.HasPermission(context.HttpContext, _permissionCode);
            if (!hasPermission)
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Home", new { permissionCode = _permissionCode });
                return;
            }

            await next();
        }
    }

    /// <summary>
    /// Вспомогательные методы для работы с авторизацией
    /// </summary>
    public static class AuthorizationHelper
    {
        private const string RequestRoleCacheKeyPrefix = "__secore.roles:";

        public static bool IsAuthenticated(HttpContext httpContext)
        {
            var organizationId = httpContext.Session.GetString("OrganizationId");
            var userId = httpContext.Session.GetString("UserId");
            return !string.IsNullOrEmpty(organizationId) && !string.IsNullOrEmpty(userId);
        }

        public static string? GetOrganizationType(HttpContext httpContext)
        {
            var rawType = httpContext.Session.GetString("OrganizationType");
            if (string.IsNullOrWhiteSpace(rawType))
                return rawType;

            var normalized = rawType.Trim().ToLowerInvariant();

            // Defensive alias: some legacy data can store "standard" instead of "standart".
            if (normalized == "standard")
                return "standart";

            return normalized;
        }

        public static string? GetOrganizationId(HttpContext httpContext)
        {
            return httpContext.Session.GetString("OrganizationId");
        }

        public static string? GetUserId(HttpContext httpContext)
        {
            return httpContext.Session.GetString("UserId");
        }

        public static string? GetUserName(HttpContext httpContext)
        {
            return httpContext.Session.GetString("UserName");
        }

        public static bool IsDoctorRole(HttpContext httpContext)
        {
            var userId = GetUserId(httpContext);
            if (string.IsNullOrEmpty(userId))
                return false;

            var cacheKey = RequestRoleCacheKeyPrefix + userId;
            if (!httpContext.Items.TryGetValue(cacheKey, out var cached) || cached is not HashSet<string> roles)
            {
                var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();
                roles = db.UserRoles
                    .Where(ur => ur.User == userId && (ur.Isdeleted == null || ur.Isdeleted == 0))
                    .Include(ur => ur.RoleNavigation)
                    .Select(ur => ur.RoleNavigation != null ? ur.RoleNavigation.Name : ur.Role)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!.Trim().ToLower())
                    .ToHashSet();

                httpContext.Items[cacheKey] = roles;
            }

            return roles.Contains("doctor") || roles.Contains("врач");
        }

        /// <summary>
        /// Ограниченный режим врача: без прав администратора и без доступа к управлению сотрудниками.
        /// </summary>
        public static bool IsDoctorOnlyMode(HttpContext httpContext)
        {
            if (!IsDoctorRole(httpContext))
                return false;

            if (IsAdminRole(httpContext))
                return false;

            if (PermissionHelper.HasPermission(httpContext, "doctors.view"))
                return false;

            return true;
        }

        /// <summary>
        /// Создание и редактирование карточек сотрудников (не для регистратуры с doctors.view).
        /// </summary>
        public static bool CanManageStaff(HttpContext httpContext)
        {
            if (PermissionHelper.HasPermission(httpContext, "doctors.edit"))
                return true;

            return IsAdminRole(httpContext)
                && string.Equals(GetOrganizationType(httpContext), "medclinic", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Создание и изменение детей, групп, услуг (не для воспитателя с children.view).
        /// </summary>
        public static bool CanManageChildren(HttpContext httpContext)
        {
            if (PermissionHelper.HasPermission(httpContext, "children.create"))
                return true;

            var orgType = GetOrganizationType(httpContext);
            return IsAdminRole(httpContext)
                && !string.Equals(orgType, "medclinic", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ручная рассылка уведомлений клиентам (не для воспитателя).
        /// </summary>
        public static bool CanSendNotifications(HttpContext httpContext)
        {
            if (PermissionHelper.HasPermission(httpContext, "notifications.send"))
                return true;

            return IsAdminRole(httpContext);
        }

        public static bool IsAdminRole(HttpContext httpContext)
        {
            var userId = GetUserId(httpContext);
            if (string.IsNullOrEmpty(userId))
                return false;

            var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();
            var cacheKey = RequestRoleCacheKeyPrefix + userId;
            if (!httpContext.Items.TryGetValue(cacheKey, out var cached) || cached is not HashSet<string> roles)
            {
                roles = db.UserRoles
                    .Where(ur => ur.User == userId && (ur.Isdeleted == null || ur.Isdeleted == 0))
                    .Include(ur => ur.RoleNavigation)
                    .Select(ur => ur.RoleNavigation != null ? ur.RoleNavigation.Name : ur.Role)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!.Trim().ToLower())
                    .ToHashSet();

                httpContext.Items[cacheKey] = roles;
            }

            if (roles.Contains("admin")
                || roles.Contains("администратор")
                || roles.Contains("superadmin")
                || roles.Contains("суперадмин"))
            {
                return true;
            }

            var legacyRole = httpContext.Session.GetString("UserRole")
                ?? db.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.Role)
                    .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(legacyRole))
                return false;

            var normalizedLegacy = legacyRole.Trim().ToLowerInvariant();
            return normalizedLegacy is "admin" or "superadmin";
        }
    }

    /// <summary>
    /// Вспомогательные методы для работы с правами доступа
    /// </summary>
    public static class PermissionHelper
    {
        private const string RequestCacheKeyPrefix = "__secore.permissions:";

        /// <summary>
        /// Проверяет, есть ли у пользователя указанное право
        /// </summary>
        public static bool HasPermission(AppDbContext db, string userId, string permissionCode)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(permissionCode))
            {
                return false;
            }

            // Получаем все роли пользователя
            var userRoles = db.UserRoles
                .Where(ur => ur.User == userId && (ur.Isdeleted == null || ur.Isdeleted == 0))
                .Select(ur => ur.Role)
                .ToList();

            if (!userRoles.Any())
            {
                return false;
            }

            // Получаем все права для этих ролей
            var permissions = db.RolePermissions
                .Where(rp => userRoles.Contains(rp.Role) && 
                            (rp.Isdeleted == null || rp.Isdeleted == 0))
                .Include(rp => rp.PermissionNavigation)
                .Select(rp => rp.PermissionNavigation)
                .Where(p => p != null && 
                           (p.Isdeleted == null || p.Isdeleted == 0) &&
                           p.Code == permissionCode)
                .Any();

            return permissions;
        }

        /// <summary>
        /// Получает все права пользователя
        /// </summary>
        public static List<string> GetUserPermissions(AppDbContext db, string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return new List<string>();
            }

            // Получаем все роли пользователя
            var userRoles = db.UserRoles
                .Where(ur => ur.User == userId && (ur.Isdeleted == null || ur.Isdeleted == 0))
                .Select(ur => ur.Role)
                .ToList();

            if (!userRoles.Any())
            {
                return new List<string>();
            }

            // Получаем все коды прав для этих ролей
            var permissions = db.RolePermissions
                .Where(rp => userRoles.Contains(rp.Role) && 
                            (rp.Isdeleted == null || rp.Isdeleted == 0))
                .Include(rp => rp.PermissionNavigation)
                .Select(rp => rp.PermissionNavigation)
                .Where(p => p != null && (p.Isdeleted == null || p.Isdeleted == 0) && !string.IsNullOrEmpty(p.Code))
                .Select(p => p.Code!)
                .Distinct()
                .ToList();

            return permissions;
        }

        /// <summary>
        /// Проверяет права доступа через HttpContext (для использования в представлениях)
        /// </summary>
        public static bool HasPermission(HttpContext httpContext, string permissionCode)
        {
            var userId = AuthorizationHelper.GetUserId(httpContext);
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            // Кэш на время одного HTTP-запроса, чтобы layout/partial'ы не били БД десятки раз.
            var cacheKey = RequestCacheKeyPrefix + userId;
            if (httpContext.Items.TryGetValue(cacheKey, out var cached) && cached is HashSet<string> set)
                return set.Contains(permissionCode);

            var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();
            var permissions = GetUserPermissions(db, userId);
            set = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
            httpContext.Items[cacheKey] = set;
            return set.Contains(permissionCode);
        }
    }
}

