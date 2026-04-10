using System.ComponentModel.DataAnnotations;

namespace WebApplication1.ViewModels.Patients;

public sealed class CreatePatientViewModel
{
    [Required]
    [Display(Name = "ФИО пациента")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "ИИН/ИНН")]
    public string? Identifier { get; set; }

    [Phone]
    [Display(Name = "Телефон")]
    public string? Phone { get; set; }

    [EmailAddress]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Display(Name = "Адрес")]
    public string? Address { get; set; }
}
