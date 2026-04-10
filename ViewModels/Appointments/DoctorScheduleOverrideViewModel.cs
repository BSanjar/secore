namespace WebApplication1.ViewModels.Appointments;

public class DoctorScheduleOverrideViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string WorkDate { get; set; } = string.Empty;
    public bool IsWorking { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? Comment { get; set; }
}
