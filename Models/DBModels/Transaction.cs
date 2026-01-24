using System;
using System.Collections.Generic;

namespace WebApplication1.Models.DBModels;

public partial class Transaction
{
    public string Id { get; set; } = null!;

    public DateTime? TransactionDate { get; set; }

    /// <summary>
    /// success
    /// error
    /// </summary>
    public string? TransactionStatus { get; set; }

    /// <summary>
    /// сумма в тыйынах
    /// </summary>
    public decimal? Summ { get; set; }

    /// <summary>
    /// сумма транзакции в тыйынах вмесе с комиссией
    /// </summary>
    public decimal? TransactionSumm { get; set; }

    public string? Invoice { get; set; }

    public string? Agent { get; set; }

    public string? PaymentInvoice { get; set; }

    public string? TxnId { get; set; }

    public string? TransactionSystem { get; set; }

    /// <summary>
    /// debit - приход
    /// credit - расход
    /// </summary>
    public string? TransactionType { get; set; }

    public virtual Invoice? InvoiceNavigation { get; set; }

    public virtual Agent? AgentNavigation { get; set; }
}
