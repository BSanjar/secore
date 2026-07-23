namespace WebApplication1.ViewModels.Payments;

public class MedclinicPaymentsIndexViewModel
{
    public string DateFrom { get; set; } = string.Empty;
    public string DateTo { get; set; } = string.Empty;
    public string Search { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string SelectedClientName { get; set; } = string.Empty;
    public string StatusFilter { get; set; } = string.Empty;
    public string ChannelFilter { get; set; } = string.Empty;

    public decimal PeriodTotalSom { get; set; }
    public int SuccessCount { get; set; }
    public int RefundCount { get; set; }
    public decimal RefundsSom { get; set; }

    public IReadOnlyList<MedclinicPaymentChannelStatViewModel> ChannelStats { get; set; } =
        Array.Empty<MedclinicPaymentChannelStatViewModel>();

    public IReadOnlyList<MedclinicPaymentRowViewModel> Rows { get; set; } =
        Array.Empty<MedclinicPaymentRowViewModel>();

    public IReadOnlyList<(string Id, string Name)> Clients { get; set; } =
        Array.Empty<(string Id, string Name)>();

    public IReadOnlyList<(string Key, string Label)> ChannelOptions { get; set; } =
        Array.Empty<(string Key, string Label)>();
}

public class MedclinicPaymentRowViewModel
{
    public string Id { get; set; } = string.Empty;
    public DateTime? TransactionDate { get; set; }
    public string PatientName { get; set; } = "—";
    public string InvoiceTitle { get; set; } = "—";
    public string PayCode { get; set; } = "—";
    public string ChannelLabel { get; set; } = "—";
    public string KindLabel { get; set; } = "Оплата";
    public decimal AmountSom { get; set; }
    public string StatusLabel { get; set; } = "—";
    public bool IsRefund { get; set; }
    public bool IsSuccess { get; set; }
}

public class MedclinicPaymentChannelStatViewModel
{
    public string Label { get; set; } = string.Empty;
    public decimal SumSom { get; set; }
    public int Count { get; set; }
}
