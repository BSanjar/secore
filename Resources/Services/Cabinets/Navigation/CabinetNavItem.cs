namespace WebApplication1.Services.Cabinets.Navigation;

public sealed record CabinetNavItem(
    string Id,
    string Label,
    string Title,
    string IconHtml,
    string? Controller = null,
    string Action = "Index",
    IReadOnlyCollection<string>? PermissionCodes = null,
    bool HideForDoctor = false,
    bool ShowForDoctorOnly = false,
    IReadOnlyCollection<CabinetNavItem>? Children = null)
{
    public bool HasChildren => Children is { Count: > 0 };
}
