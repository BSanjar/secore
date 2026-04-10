namespace WebApplication1.ViewModels.Departments;

public class SpecializationListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public int DoctorsCount { get; set; }
}
