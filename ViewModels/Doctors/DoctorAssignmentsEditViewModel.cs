using WebApplication1.Dtos;
using WebApplication1.ViewModels.Appointments;

namespace WebApplication1.ViewModels.Doctors;

public class DoctorAssignmentsEditViewModel
{
    public DoctorAssignmentsUpsertDto Form { get; set; } = new();
    public SaveUserWorkScheduleRequest ScheduleForm { get; set; } = new();
    public SaveUserWorkScheduleOverrideRequest OverrideForm { get; set; } = new();
    public string DoctorId { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<DoctorOptionViewModel> Departments { get; set; } = Array.Empty<DoctorOptionViewModel>();
    public IReadOnlyList<DoctorOptionViewModel> Specializations { get; set; } = Array.Empty<DoctorOptionViewModel>();
    public IReadOnlyList<DoctorScheduleViewModel> DoctorSchedules { get; set; } = Array.Empty<DoctorScheduleViewModel>();
    public IReadOnlyList<DoctorScheduleOverrideViewModel> DoctorScheduleOverrides { get; set; } = Array.Empty<DoctorScheduleOverrideViewModel>();
    public IReadOnlyList<AppointmentCalendarEventViewModel> Events { get; set; } = Array.Empty<AppointmentCalendarEventViewModel>();
    public int AppointmentDurationMinutes { get; set; } = 30;
    public bool DoctorSchedulesReady { get; set; }
    public string? DoctorSchedulesMessage { get; set; }
    public bool DoctorScheduleOverridesReady { get; set; }
    public string? DoctorScheduleOverridesMessage { get; set; }
}
