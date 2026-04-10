using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Dtos;

public class SpecializationUpsertDto
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Укажите название специализации.")]
    [StringLength(200, ErrorMessage = "Название специализации не должно превышать 200 символов.")]
    public string Name { get; set; } = string.Empty;

    public string? DepartmentId { get; set; }

    [StringLength(1000, ErrorMessage = "Описание не должно превышать 1000 символов.")]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
