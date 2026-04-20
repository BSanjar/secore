namespace WebApplication1.ViewModels.Appointments;

public class AppointmentsIndexViewModel
{
    public int PatientsCount { get; set; }
    public int DoctorsCount { get; set; }
    public bool HasPatients => PatientsCount > 0;
    public bool HasDoctors => DoctorsCount > 0;
    public IReadOnlyList<AppointmentCalendarEventViewModel> Events { get; set; } = Array.Empty<AppointmentCalendarEventViewModel>();
    public IReadOnlyList<SelectOptionViewModel> Doctors { get; set; } = Array.Empty<SelectOptionViewModel>();
    public IReadOnlyList<SelectOptionViewModel> Departments { get; set; } = Array.Empty<SelectOptionViewModel>();
    public IReadOnlyList<AppointmentDoctorFilterOptionViewModel> DoctorFilters { get; set; } = Array.Empty<AppointmentDoctorFilterOptionViewModel>();
    public IReadOnlyList<PatientLookupViewModel> Patients { get; set; } = Array.Empty<PatientLookupViewModel>();
    public IReadOnlyList<SelectOptionViewModel> Services { get; set; } = Array.Empty<SelectOptionViewModel>();
    public IReadOnlyList<AppointmentServiceCatalogItemViewModel> ServiceCatalog { get; set; } = Array.Empty<AppointmentServiceCatalogItemViewModel>();
    public IReadOnlyList<AppointmentMedicalTemplateViewModel> MedicalTemplates { get; set; } = Array.Empty<AppointmentMedicalTemplateViewModel>();
    public IReadOnlyList<DoctorScheduleViewModel> DoctorSchedules { get; set; } = Array.Empty<DoctorScheduleViewModel>();
    public IReadOnlyList<DoctorScheduleOverrideViewModel> DoctorScheduleOverrides { get; set; } = Array.Empty<DoctorScheduleOverrideViewModel>();
    public IReadOnlyList<AppointmentDurationSettingViewModel> AppointmentDurations { get; set; } = Array.Empty<AppointmentDurationSettingViewModel>();
    public bool StorageReady { get; set; }
    public string? StorageMessage { get; set; }
    public bool DoctorSchedulesReady { get; set; }
    public string? DoctorSchedulesMessage { get; set; }
}
