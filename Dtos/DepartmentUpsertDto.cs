using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Dtos;

public class DepartmentUpsertDto
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Укажите название отделения.")]
    [StringLength(200, ErrorMessage = "Название отделения не должно превышать 200 символов.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "Код не должен превышать 50 символов.")]
    public string? Code { get; set; }

    [StringLength(1000, ErrorMessage = "Описание не должно превышать 1000 символов.")]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public List<string> SpecializationIds { get; set; } = new();
}
