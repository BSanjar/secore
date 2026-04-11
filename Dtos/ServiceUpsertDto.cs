using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Dtos;

public class ServiceUpsertDto
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Укажите название услуги.")]
    [StringLength(200, ErrorMessage = "Название услуги не должно превышать 200 символов.")]
    public string Name { get; set; } = string.Empty;

    public bool IsFixed { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Сумма не может быть отрицательной.")]
    public decimal? FixedAmount { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Сумма не может быть отрицательной.")]
    public decimal? MinAmount { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Сумма не может быть отрицательной.")]
    public decimal? MaxAmount { get; set; }

    public List<string> SpecializationIds { get; set; } = new();
}
