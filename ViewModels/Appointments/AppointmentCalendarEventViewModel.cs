namespace WebApplication1.ViewModels.Appointments;

public class AppointmentCalendarEventViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? DoctorId { get; set; }
    public string Doctor { get; set; } = string.Empty;
    public string? PatientId { get; set; }
    public string? PatientName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? PatientGender { get; set; }
    public string? PatientBirthDate { get; set; }
    public int? PatientAge { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Status { get; set; } = "busy";
    public string Notes { get; set; } = string.Empty;
    public string? ReferralSource { get; set; }
    public string? PaymentType { get; set; }
    public bool HasPaidInvoice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IReadOnlyList<AppointmentServiceLineViewModel> Services { get; set; } = Array.Empty<AppointmentServiceLineViewModel>();
}
