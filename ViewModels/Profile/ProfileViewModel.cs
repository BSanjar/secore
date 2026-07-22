using System.Collections.Generic;

namespace WebApplication1.ViewModels.Profile;

public sealed class ProfileViewModel
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? OrganizationName { get; set; }
    public string? OrganizationType { get; set; }
    public string StartPageLabel { get; set; } = string.Empty;
    public DateTime? LastLogin { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public string Initials { get; set; } = "?";
}
