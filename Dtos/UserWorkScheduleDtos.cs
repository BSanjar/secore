namespace WebApplication1.Dtos;

public class SaveUserWorkScheduleRequest
{
    public string? UserId { get; set; }
    public List<int> DaysOfWeek { get; set; } = new();
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
}

public class SaveUserWorkScheduleOverrideRequest
{
    public string? UserId { get; set; }
    public DateTime? WorkDate { get; set; }
    public string Mode { get; set; } = "default";
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? Comment { get; set; }
}
