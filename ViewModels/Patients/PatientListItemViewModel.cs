namespace WebApplication1.ViewModels.Patients;

public sealed class PatientListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Identifier { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public DateTime? CreatedDate { get; set; }
}
