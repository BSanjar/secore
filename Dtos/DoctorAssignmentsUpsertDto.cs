using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Dtos;

public class DoctorAssignmentsUpsertDto
{
    [Required]
    public string Id { get; set; } = string.Empty;

    public List<string> SelectedDepartmentIds { get; set; } = new();
    public List<string> SelectedSpecializationIds { get; set; } = new();

    public string? PrimaryDepartmentId { get; set; }
    public string? PrimarySpecializationId { get; set; }
}
