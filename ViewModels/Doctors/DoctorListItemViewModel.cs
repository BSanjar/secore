namespace WebApplication1.ViewModels.Doctors;

public class DoctorListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Departments { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Specializations { get; set; } = Array.Empty<string>();
}
