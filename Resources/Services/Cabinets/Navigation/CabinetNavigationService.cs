namespace WebApplication1.Services.Cabinets.Navigation;

public interface ICabinetNavigationService
{
    IReadOnlyList<CabinetNavItem> BuildMain(string variant, IReadOnlyCollection<string> userPermissions, bool isDoctorRole);
    IReadOnlyList<CabinetNavItem> BuildSecondary(string variant, IReadOnlyCollection<string> userPermissions, bool isDoctorRole);
}

public sealed class CabinetNavigationService : ICabinetNavigationService
{
    public IReadOnlyList<CabinetNavItem> BuildMain(string variant, IReadOnlyCollection<string> userPermissions, bool isDoctorRole)
    {
        var normalizedVariant = (variant ?? string.Empty).Trim().ToLowerInvariant();
        var allItems = normalizedVariant switch
        {
            "medclinic" => BuildMedclinicItems(),
            _ => BuildDetsadItems()
        };

        return FilterVisible(allItems, userPermissions, isDoctorRole);
    }

    public IReadOnlyList<CabinetNavItem> BuildSecondary(string variant, IReadOnlyCollection<string> userPermissions, bool isDoctorRole)
    {
        var normalizedVariant = (variant ?? string.Empty).Trim().ToLowerInvariant();
        var allItems = normalizedVariant switch
        {
            "medclinic" => BuildMedclinicSecondaryItems(),
            _ => BuildDetsadSecondaryItems()
        };

        return FilterVisible(allItems, userPermissions, isDoctorRole);
    }

    private static IReadOnlyList<CabinetNavItem> BuildDetsadItems()
    {
        return new List<CabinetNavItem>
        {
            new(
                Id: "children",
                Label: "Children",
                Title: "Children",
                IconHtml: "<svg width=\"18\" height=\"18\" viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z\" fill=\"currentColor\" /></svg>",
                Controller: "Clients",
                PermissionCodes: new[] { "nav.children" }),
            new(
                Id: "services",
                Label: "Services",
                Title: "Services",
                IconHtml: "<svg width=\"18\" height=\"18\" viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M19 4h-1V2h-2v2H8V2H6v2H5c-1.11 0-1.99.9-1.99 2L3 20c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 16H5V9h14v11zM9 11H7v2h2v-2zm4 0h-2v2h2v-2zm4 0h-2v2h2v-2z\" fill=\"currentColor\" /></svg>",
                Controller: "OrganizationServices",
                PermissionCodes: new[] { "nav.services" }),
            new(
                Id: "groups",
                Label: "Groups",
                Title: "Groups",
                IconHtml: "<svg width=\"18\" height=\"18\" viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M12 7V3H2v18h20V7H12zM6 19H4v-2h2v2zm0-4H4v-2h2v2zm0-4H4V9h2v2zm0-4H4V5h2v2zm4 12H8v-2h2v2zm0-4H8v-2h2v2zm0-4H8V9h2v2zm0-4H8V5h2v2zm10 12h-8v-2h2v-2h-2v-2h2v-2h-2V9h8v10zm-2-8h-2v2h2v-2zm0 4h-2v2h2v-2z\" fill=\"currentColor\" /></svg>",
                Controller: "OrgClientGroups",
                PermissionCodes: new[] { "nav.groups" }),
            new(
                Id: "invoices",
                Label: "Invoices",
                Title: "Invoices",
                IconHtml: "<svg width=\"18\" height=\"18\" viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M20 4H4c-1.11 0-1.99.89-1.99 2L2 18c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V6c0-1.1-.9-2-2-2zm0 14H4v-6h16v6zm0-10H4V6h16v2z\" fill=\"currentColor\" /></svg>",
                Controller: "Invoices",
                PermissionCodes: new[] { "nav.invoices" }),
            new(
                Id: "payments",
                Label: "Payments",
                Title: "Payments",
                IconHtml: "<svg width=\"18\" height=\"18\" viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm.31-8.86c-1.77-.45-2.34-.94-2.34-1.67 0-.84.79-1.43 2.1-1.43 1.38 0 1.9.66 1.94 1.64h1.71c-.05-1.34-.87-2.57-2.49-2.97V5H10.9v1.69c-1.51.32-2.72 1.3-2.72 2.81 0 1.79 1.49 2.69 3.66 3.21 1.95.46 2.34 1.15 2.34 1.87 0 .53-.39 1.39-2.1 1.39-1.6 0-2.23-.72-2.32-1.64H8.04c.1 1.7 1.36 2.66 2.86 2.97V19h2.34v-1.67c1.52-.29 2.72-1.16 2.73-2.77-.01-2.2-1.9-2.96-3.66-3.42z\" fill=\"currentColor\" /></svg>",
                Controller: "Payments",
                PermissionCodes: new[] { "nav.transactions" })
        };
    }

    private static IReadOnlyList<CabinetNavItem> BuildMedclinicItems()
    {
        return new List<CabinetNavItem>
        {
            new(
                Id: "home",
                Label: "Главная",
                Title: "Главная",
                IconHtml: "&#x2302;",
                Controller: "Cabinet",
                PermissionCodes: new[] { "dashboard.view", "nav.dashboard", "nav.home" },
                HideForDoctor: true),
            new(
                Id: "appointments-doctor",
                Label: "Мой график",
                Title: "Мой график",
                IconHtml: "&#x25F7;",
                Controller: "Appointments",
                Action: "Doctor",
                PermissionCodes: new[] { "appointments.doctor.view", "appointments.view", "nav.appointments" },
                ShowForDoctorOnly: true),
            new(
                Id: "appointments-registry",
                Label: "Реестр приемов",
                Title: "Реестр приемов",
                IconHtml: "&#x25A6;",
                Controller: "Appointments",
                Action: "Registry",
                PermissionCodes: new[] { "appointments.registry.view", "appointments.view", "nav.appointments" },
                HideForDoctor: true),
            new(
                Id: "doctors",
                Label: "Доктора",
                Title: "Доктора",
                IconHtml: "<svg width=\"18\" height=\"18\" viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M19 8h-3V5a4 4 0 1 0-8 0v3H5a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-9a2 2 0 0 0-2-2zm-9 0V5a2 2 0 1 1 4 0v3h-4zm8 11H6v-9h12v9z\" fill=\"currentColor\"/></svg>",
                Controller: "Doctors",
                PermissionCodes: new[] { "doctors.view", "nav.doctors" },
                HideForDoctor: true),
            new(
                Id: "tables",
                Label: "Таблицы",
                Title: "Таблицы",
                IconHtml: "&#x229E;",
                HideForDoctor: true,
                Children: new List<CabinetNavItem>
                {
                    new("departments", "Отделения", "Отделения", "&#x25A3;", "Departments", PermissionCodes: new[] { "departments.view", "nav.departments" }),
                    new("specializations", "Специализации", "Специализации", "&#x25CE;", "Specializations", PermissionCodes: new[] { "specializations.view", "nav.specializations" }),
                    new("patients", "Пациенты", "Пациенты", "&#x25C9;", "Patients", PermissionCodes: new[] { "patients.view", "nav.patients" }),
                    new("services", "Услуги", "Услуги", "&#x271A;", "Services", PermissionCodes: new[] { "services.view", "orgservices.view", "nav.services" }),
                    new("appointment-templates", "Шаблоны приемов", "Шаблоны приемов", "&#x270E;", "Appointments", "Templates", PermissionCodes: new[] { "appointments.edit" })
                }),
            new(
                Id: "invoices",
                Label: "Счета",
                Title: "Счета",
                IconHtml: "&#x21C4;",
                Controller: "Invoices",
                PermissionCodes: new[] { "invoices.view", "nav.invoices", "transactions.view", "nav.transactions" }),
            new(
                Id: "notifications",
                Label: "Уведомления",
                Title: "Уведомления",
                IconHtml: "&#x25D4;",
                Controller: "Notifications",
                PermissionCodes: new[] { "notifications.view", "nav.notifications" },
                HideForDoctor: true)
        };
    }

    private static IReadOnlyList<CabinetNavItem> BuildDetsadSecondaryItems()
    {
        return new List<CabinetNavItem>
        {
            new(
                Id: "settings",
                Label: "Settings",
                Title: "Settings",
                IconHtml: "&#x2699;",
                Controller: "Settings",
                PermissionCodes: new[] { "nav.settings", "settings.view" }),
            new(
                Id: "support",
                Label: "Support",
                Title: "Support",
                IconHtml: "?",
                Controller: "Support",
                PermissionCodes: new[] { "nav.support", "support.view" }),
            new(
                Id: "profile",
                Label: "Profile",
                Title: "Profile",
                IconHtml: "&#x25D0;",
                Controller: "Profile",
                PermissionCodes: new[] { "nav.profile", "profile.view" })
        };
    }

    private static IReadOnlyList<CabinetNavItem> BuildMedclinicSecondaryItems()
    {
        return new List<CabinetNavItem>
        {
            new(
                Id: "profile",
                Label: "Профиль",
                Title: "Профиль",
                IconHtml: "&#x25D0;",
                Controller: "Profile",
                PermissionCodes: new[] { "profile.view", "nav.profile" },
                HideForDoctor: true),
            new(
                Id: "settings",
                Label: "Настройки",
                Title: "Настройки",
                IconHtml: "&#x2699;",
                Controller: "Settings",
                PermissionCodes: new[] { "settings.view", "nav.settings" },
                HideForDoctor: true)
        };
    }

    private static IReadOnlyList<CabinetNavItem> FilterVisible(
        IReadOnlyList<CabinetNavItem> items,
        IReadOnlyCollection<string> userPermissions,
        bool isDoctorRole)
    {
        var filtered = new List<CabinetNavItem>();
        foreach (var item in items)
        {
            if (item.HideForDoctor && isDoctorRole)
            {
                continue;
            }
            if (item.ShowForDoctorOnly && !isDoctorRole)
            {
                continue;
            }

            var visibleChildren = item.HasChildren
                ? FilterVisible(item.Children!.ToList(), userPermissions, isDoctorRole)
                : Array.Empty<CabinetNavItem>();

            var selfVisible = HasAnyPermission(userPermissions, item.PermissionCodes);
            if (item.HasChildren)
            {
                if (visibleChildren.Count == 0)
                {
                    continue;
                }

                filtered.Add(item with { Children = visibleChildren });
                continue;
            }

            if (!selfVisible)
            {
                continue;
            }

            filtered.Add(item);
        }

        return filtered;
    }

    private static bool HasAnyPermission(IReadOnlyCollection<string> userPermissions, IReadOnlyCollection<string>? codes)
    {
        if (codes == null || codes.Count == 0)
        {
            return true;
        }

        return codes.Any(code => userPermissions.Contains(code, StringComparer.OrdinalIgnoreCase));
    }
}
