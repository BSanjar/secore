using WebApplication1.Dtos;
using WebApplication1.ViewModels.Appointments;

namespace WebApplication1.ViewModels.Doctors;

public class StaffCardEditViewModel
{
    public StaffCardSaveDto Form { get; set; } = new();

    public string StaffId { get; set; } = string.Empty;
    public string StaffName { get; set; } = string.Empty;

    public IReadOnlyList<DoctorOptionViewModel> AvailableRoles { get; set; } = Array.Empty<DoctorOptionViewModel>();
    public IReadOnlyList<DoctorOptionViewModel> Departments { get; set; } = Array.Empty<DoctorOptionViewModel>();
    public IReadOnlyList<DoctorOptionViewModel> Specializations { get; set; } = Array.Empty<DoctorOptionViewModel>();

    public IReadOnlyList<DoctorScheduleViewModel> WorkSchedules { get; set; } = Array.Empty<DoctorScheduleViewModel>();
    public IReadOnlyList<DoctorScheduleOverrideViewModel> ScheduleOverrides { get; set; } = Array.Empty<DoctorScheduleOverrideViewModel>();

    public bool WorkSchedulesReady { get; set; }
    public string? WorkSchedulesMessage { get; set; }
    public bool ScheduleOverridesReady { get; set; }
    public string? ScheduleOverridesMessage { get; set; }

    public bool CanEdit { get; set; }
}
