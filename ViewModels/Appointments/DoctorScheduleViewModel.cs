namespace WebApplication1.ViewModels.Appointments;

public class DoctorScheduleViewModel
{
    public string DoctorId { get; set; } = string.Empty;
    public int DayOfWeek { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
}
