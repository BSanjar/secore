namespace WebApplication1.Services.Cabinets;

public interface IShellViewModelBuilder
{
    ShellViewModel Build();
}

public sealed class ShellViewModelBuilder : IShellViewModelBuilder
{
    private readonly ICurrentTenantService _currentTenantService;

    public ShellViewModelBuilder(ICurrentTenantService currentTenantService)
    {
        _currentTenantService = currentTenantService;
    }

    public ShellViewModel Build()
    {
        var profile = _currentTenantService.GetCurrent().Profile;
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
            "medclinic" => "OrganizationProfileMedclinic",
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

        return new ShellViewModel(
            BodyClass: bodyClass,
            TitleSuffixResourceKey: titleSuffixResourceKey,
            OrganizationCaptionResourceKey: organizationCaptionResourceKey,
            IncludeAntiforgery: true,
            HeaderClass: headerClass,
            MainContainerClass: mainContainerClass,
            DashboardMainContainerClass: dashboardMainContainerClass,
            DashboardFooterContainerClass: dashboardFooterContainerClass,
            CabinetCssPath: cabinetCssPath,
            IsSimple: isSimple);
    }
}
