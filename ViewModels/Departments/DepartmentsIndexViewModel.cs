namespace WebApplication1.ViewModels.Departments;

public class DepartmentsIndexViewModel
{
    public bool StorageReady { get; set; }
    public string? StorageMessage { get; set; }
    public int DepartmentsCount { get; set; }
    public int SpecializationsCount { get; set; }
    public int DoctorsLinkedCount { get; set; }
    public IReadOnlyList<DepartmentListItemViewModel> Departments { get; set; } = Array.Empty<DepartmentListItemViewModel>();
    public IReadOnlyList<SpecializationListItemViewModel> Specializations { get; set; } = Array.Empty<SpecializationListItemViewModel>();
}
