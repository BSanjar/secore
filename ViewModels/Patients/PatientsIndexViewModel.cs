namespace WebApplication1.ViewModels.Patients;

public sealed class PatientsIndexViewModel
{
    public string Search { get; set; } = string.Empty;
    public IReadOnlyCollection<PatientListItemViewModel> Patients { get; set; } = Array.Empty<PatientListItemViewModel>();
}
