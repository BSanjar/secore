namespace WebApplication1.Services.Cabinets;

public sealed record CabinetProfile(
    string Key,
    string OrganizationType,
    string LayoutPath,
    IReadOnlyCollection<string> Features)
{
    public bool HasFeature(string feature) => Features.Contains(feature, StringComparer.OrdinalIgnoreCase);
}
