namespace WebApplication1.ViewModels.Appointments;

public class AppointmentTemplatesIndexViewModel
{
    public bool StorageReady { get; set; }
    public string? StorageMessage { get; set; }
    public IReadOnlyList<AppointmentMedicalTemplateViewModel> Templates { get; set; } = Array.Empty<AppointmentMedicalTemplateViewModel>();
}

