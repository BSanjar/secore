namespace WebApplication1.Dtos;

public class BulkImportResult
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public List<string> Errors { get; set; } = new();

    public bool Success => Errors.Count == 0;

    public string ToMessage(string entityName)
    {
        var message = $"{entityName}: создано {Created}, обновлено {Updated}, пропущено {Skipped}.";
        if (Errors.Count > 0)
            message += " Ошибки: " + string.Join("; ", Errors.Take(5));
        return message;
    }
}
