namespace WebApplication1.ViewModels.Departments;

public class DepartmentListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SpecializationsCount { get; set; }
    public int DoctorsCount { get; set; }
}
