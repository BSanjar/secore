namespace WebApplication1.ViewModels.Appointments;

public class AppointmentDoctorFilterOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public IReadOnlyList<string> DepartmentIds { get; set; } = Array.Empty<string>();
}
