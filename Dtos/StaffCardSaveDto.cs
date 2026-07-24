using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Dtos;

public class StaffCardSaveDto
{
    [Required(ErrorMessage = "Укажите идентификатор сотрудника.")]
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите имя сотрудника.")]
    [StringLength(200, ErrorMessage = "Имя не должно превышать 200 символов.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите email.")]
    [EmailAddress(ErrorMessage = "Укажите корректный email.")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Укажите корректный телефон.")]
    public string? Phone { get; set; }

    public string? Password { get; set; }

    public List<string> SelectedRoleIds { get; set; } = new();

    public List<string> SelectedDepartmentIds { get; set; } = new();

    public List<string> SelectedSpecializationIds { get; set; } = new();

    public string? PrimaryDepartmentId { get; set; }

    public string? PrimarySpecializationId { get; set; }

    public List<int> DaysOfWeek { get; set; } = new();

    public string? StartTime { get; set; }

    public string? EndTime { get; set; }

    [Range(5, 240, ErrorMessage = "Длительность приёма должна быть от 5 до 240 минут.")]
    public int AppointmentDurationMinutes { get; set; } = 30;

    public DateTime? OverrideWorkDate { get; set; }

    public string OverrideMode { get; set; } = "default";

    public string? OverrideStartTime { get; set; }

    public string? OverrideEndTime { get; set; }

    public string? OverrideComment { get; set; }
}
