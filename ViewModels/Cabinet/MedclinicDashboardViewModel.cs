namespace WebApplication1.ViewModels.Cabinet;

public class MedclinicDashboardViewModel
{
    public string OrganizationName { get; set; } = "Medclinic";
    public string TodayTitle { get; set; } = string.Empty;

    public int AppointmentsTotalToday { get; set; }
    public int AppointmentsAcceptedToday { get; set; }
    public int AppointmentsRemainingToday { get; set; }

    public bool ShowNewAppointmentButton { get; set; }
    public string? NewAppointmentUrl { get; set; }

    public bool CanViewPaymentSums { get; set; }
    public decimal TodayTransactionsSumSom { get; set; }

    public IReadOnlyList<MedclinicDashboardTransactionViewModel> Transactions { get; set; } =
        Array.Empty<MedclinicDashboardTransactionViewModel>();

    public bool HasSubscription { get; set; }
    public string BillingStatus { get; set; } = "Комиссионный";
    public string CommissionKindText { get; set; } = "—";
    public string CommissionValueText { get; set; } = "—";
    public string CommissionModelName { get; set; } = "—";

    public string ChartWeekJson { get; set; } = "[]";
    public string ChartMonthJson { get; set; } = "[]";
    public string ChartYearJson { get; set; } = "[]";
}
