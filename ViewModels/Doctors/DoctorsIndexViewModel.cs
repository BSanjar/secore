namespace WebApplication1.ViewModels.Doctors;

public class DoctorsIndexViewModel
{
    public string? Search { get; set; }
    public IReadOnlyList<DoctorListItemViewModel> Doctors { get; set; } = Array.Empty<DoctorListItemViewModel>();
}
