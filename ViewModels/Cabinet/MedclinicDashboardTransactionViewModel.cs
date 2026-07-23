namespace WebApplication1.ViewModels.Cabinet;

public class MedclinicDashboardTransactionViewModel
{
    public string Id { get; set; } = string.Empty;
    public string PatientName { get; set; } = "—";
    public string Description { get; set; } = "Платёж";
    public DateTime? TransactionDate { get; set; }
    public decimal AmountSom { get; set; }
    public string StatusLabel { get; set; } = "Успешно";
    public string KindLabel { get; set; } = "Оплата";
    public bool IsCredit { get; set; }
}
