namespace WebApplication1.ViewModels.Services;

public class ServiceListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal? FixedAmount { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public bool IsFixed { get; set; }
    public IReadOnlyList<string> SpecializationNames { get; set; } = Array.Empty<string>();
}
