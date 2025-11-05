using System;
using System.Collections.Generic;

namespace WebApplication1.Models.DBModels;

/// <summary>
/// график долгов клиента по инвойсу
/// </summary>
public partial class InvoicePayment
{
    public string Id { get; set; } = null!;

    public string? Invoice { get; set; }

    public DateTime? DeadlinePayDate { get; set; }

    /// <summary>
    /// сумма долга
    /// </summary>
    public string? PaymentSumm { get; set; }

    /// <summary>
    /// paid
    /// non_paid
    /// anulated - в таком случае д\с возвращается обратно на баланс по инвойсу
    /// 
    /// </summary>
    public string? PaymentStatus { get; set; }
}
