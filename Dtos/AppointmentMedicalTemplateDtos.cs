namespace WebApplication1.Dtos;

public class SaveAppointmentMedicalTemplateRequest
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public class DeleteAppointmentMedicalTemplateRequest
{
    public string? Id { get; set; }
}
