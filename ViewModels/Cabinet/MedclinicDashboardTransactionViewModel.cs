namespace WebApplication1.ViewModels.Cabinet;

public class MedclinicDashboardTransactionViewModel
{
    public string Id { get; set; } = string.Empty;
    public string PatientName { get; set; } = "—";
    public string Description { get; set; } = "Платёж";
    public DateTime? TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}