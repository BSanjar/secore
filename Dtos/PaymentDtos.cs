using System;

namespace WebApplication1.Dtos;

public class CreatePaymentViewModel
{
    public string? ClientId { get; set; }
    public decimal AmountSom { get; set; }
    public string? InvoiceName { get; set; }
    public string? PayCode { get; set; }
}

public class PaymentListItemVm
{
    public string TransactionId { get; set; } = null!;
    public DateTime? Date { get; set; }
    public string? ClientName { get; set; }
    public string? InvoiceName { get; set; }
    public decimal AmountSom { get; set; }
    public string? Status { get; set; }
}

public class SimplePaymentsIndexViewModel
{
    public GenericTableViewModel<object> Table { get; set; } = new();
    public string CreatePaymentUrl { get; set; } = string.Empty;
    public string? FlashMessage { get; set; }
}

public class SimplePaymentsFilterParams : BaseFilterParams
{
    public string? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}
