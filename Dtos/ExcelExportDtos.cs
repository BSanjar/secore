using System;

namespace WebApplication1.Dtos;

public class TransactionExcelRow
{
    public DateTime? Date { get; set; }
    public string? ClientName { get; set; }
    public string? PayCode { get; set; }
    public string? AgentName { get; set; }
    public decimal? AmountTyiyn { get; set; }
    public decimal? TotalTyiyn { get; set; }
    public string? Status { get; set; }
}

/// <summary>Строка выгрузки платежей по счетам (из InvoicePayments).</summary>
public class InvoicePaymentExcelRow
{
    public string? InvoiceId { get; set; }
    public string? PayCode { get; set; }
    public string? ClientName { get; set; }
    public DateTime? PeriodFrom { get; set; }
    public DateTime? PeriodTo { get; set; }
    public string? PeriodValue { get; set; }
    public decimal? AmountTyiyn { get; set; }
    public string? PaymentStatus { get; set; }
}

