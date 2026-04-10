namespace WebApplication1.ViewModels.Cabinet;

public class MedclinicDashboardViewModel
{
    public IReadOnlyList<MedclinicDashboardMetricViewModel> Metrics { get; set; } = Array.Empty<MedclinicDashboardMetricViewModel>();
    public IReadOnlyList<MedclinicDashboardTransactionViewModel> Transactions { get; set; } = Array.Empty<MedclinicDashboardTransactionViewModel>();
    public string OrganizationName { get; set; } = "Medclinic";
    public bool HasSubscription { get; set; }
    public string BillingStatus { get; set; } = "Комиссионный";
    public string CommissionKindText { get; set; } = "—";
    public string CommissionValueText { get; set; } = "—";
    public string CommissionModelName { get; set; } = "—";
    public string ChartWeekJson { get; set; } = "[]";
    public string ChartMonthJson { get; set; } = "[]";
    public string ChartYearJson { get; set; } = "[]";
}
