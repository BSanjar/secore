using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Models.DBModels;

/// <summary>
/// записи - за какие периоды оплачены
/// </summary>
[Table("invoice_payments")]
public partial class InvoicePayment
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("invoice", TypeName = "character varying")]
    public string? Invoice { get; set; }

    [Column("date_to", TypeName = "timestamp without time zone")]
    public DateTime? DateTo { get; set; }

    /// <summary>
    /// сумма оплаты
    /// </summary>
    [Column("payment_summ", TypeName = "character varying")]
    public string? PaymentSumm { get; set; }

    /// <summary>
    /// paid - уже оплатил
    /// non_paid - еще не оплатил
    /// anulated - в таком случае д\с возвращается обратно на баланс по инвойсу
    /// </summary>
    [Column("payment_status", TypeName = "character varying")]
    public string? PaymentStatus { get; set; }

    [Column("date_from", TypeName = "timestamp without time zone")]
    public DateTime? DateFrom { get; set; }

    /// <summary>
    /// какой месяц или год
    /// если периодичность месяц или год
    /// </summary>
    [Column("period_value", TypeName = "character varying")]
    public string? PeriodValue { get; set; }

    [ForeignKey("Invoice")]
    [InverseProperty("InvoicePayments")]
    public virtual Invoice? InvoiceNavigation { get; set; }
}
