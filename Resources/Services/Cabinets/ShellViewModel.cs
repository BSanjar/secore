namespace WebApplication1.Services.Cabinets;

public sealed record ShellViewModel(
    string BodyClass,
    string TitleSuffixResourceKey,
    string? OrganizationCaptionResourceKey,
    string? OrganizationDisplayName,
    bool IncludeAntiforgery,
    string HeaderClass,
    string MainContainerClass,
    string DashboardMainContainerClass,
    string DashboardFooterContainerClass,
    string CabinetCssPath,
    bool IsSimple);
