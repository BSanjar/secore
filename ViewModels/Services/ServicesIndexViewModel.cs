namespace WebApplication1.ViewModels.Services;

public class ServicesIndexViewModel
{
    public string? Search { get; set; }
    public IReadOnlyList<ServiceListItemViewModel> Services { get; set; } = Array.Empty<ServiceListItemViewModel>();
}
