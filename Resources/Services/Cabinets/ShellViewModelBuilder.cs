namespace WebApplication1.Services.Cabinets;

using Microsoft.EntityFrameworkCore;

public interface IShellViewModelBuilder
{
    ShellViewModel Build();
}

public sealed class ShellViewModelBuilder : IShellViewModelBuilder
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly WebApplication1.Models.DBModels.AppDbContext _db;

    public ShellViewModelBuilder(
        ICurrentTenantService currentTenantService,
        WebApplication1.Models.DBModels.AppDbContext db)
    {
        _currentTenantService = currentTenantService;
        _db = db;
    }

    public ShellViewModel Build()
    {
        var tenant = _currentTenantService.GetCurrent();
        var profile = tenant.Profile;
        var profileKey = (profile.Key ?? string.Empty).Trim().ToLowerInvariant();
        var isSimple = string.Equals(profileKey, "simple", StringComparison.OrdinalIgnoreCase);

        var bodyClass = profileKey switch
        {
            "medclinic" => "medclinic-shell org-medclinic",
            "detsad" => "cabinet-body dashboard-body org-detsad",
            "standart" => "cabinet-body dashboard-body org-standart",
            _ => "cabinet-body org-standart"
        };
        var titleSuffixResourceKey = profileKey == "medclinic"
            ? "MedicalCabinet"
            : "PersonalCabinet";
        var organizationCaptionResourceKey = profileKey switch
        {
            "medclinic" => null,
            "detsad" => "OrganizationProfileDetsad",
            _ => null
        };

        var headerClass = isSimple ? "cabinet-header simple-header" : "cabinet-header";
        var mainContainerClass = isSimple
            ? "container app-shell-content-wrap"
            : "container-fluid app-shell-content-wrap app-shell-content-wrap--fluid";
        var dashboardMainContainerClass = "container app-shell-content-wrap";
        var dashboardFooterContainerClass = "container-fluid px-3 px-md-4 app-shell-content-wrap app-shell-content-wrap--fluid";
        var cabinetCssPath = profileKey switch
        {
            "simple" => "~/web/Simple/css/cabinet.css",
            "medclinic" => "~/web/medclinic/css/medclinic-layout.css",
            "standart" => "~/web/standart/css/cabinet.css",
            _ => "~/web/kindergarten/css/cabinet.css"
        };

        string? organizationDisplayName = null;
        if (profileKey == "medclinic" && !string.IsNullOrWhiteSpace(tenant.OrganizationId))
        {
            organizationDisplayName = _db.Organizations
                .AsNoTracking()
                .Where(o => o.Id == tenant.OrganizationId)
                .Select(o => o.Name)
                .FirstOrDefault();
        }

        return new ShellViewModel(
            BodyClass: bodyClass,
            TitleSuffixResourceKey: titleSuffixResourceKey,
            OrganizationCaptionResourceKey: organizationCaptionResourceKey,
            OrganizationDisplayName: organizationDisplayName,
            IncludeAntiforgery: true,
            HeaderClass: headerClass,
            MainContainerClass: mainContainerClass,
            DashboardMainContainerClass: dashboardMainContainerClass,
            DashboardFooterContainerClass: dashboardFooterContainerClass,
            CabinetCssPath: cabinetCssPath,
            IsSimple: isSimple);
    }
}
