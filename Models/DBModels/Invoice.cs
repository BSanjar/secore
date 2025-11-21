using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Models.DBModels;

[Table("invoice")]
public partial class Invoice
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("date_created", TypeName = "timestamp without time zone")]
    public DateTime? DateCreated { get; set; }

    [Column("user_creater", TypeName = "character varying")]
    public string? UserCreater { get; set; }

    /// <summary>
    /// actual
    /// suspended
    /// closed
    /// </summary>
    [Column("invoice_status", TypeName = "character varying")]
    public string? InvoiceStatus { get; set; }

    /// <summary>
    /// периодичность оплаты:
    /// daily - ежедневно
    /// weekly - еженедельно
    /// monthly - ежемесячно
    /// yearly - ежегодно
    /// если указывается конкретное число то значит каждые указанное число дней. 
    /// т.е если к примеру 30 то каждые 30 дней.
    /// oneTime - однаразовый и прием в любой момент
    /// any - прием в любой момент
    /// </summary>
    [Column("periodicity", TypeName = "character varying")]
    public string? Periodicity { get; set; }

    /// <summary>
    /// Дата начал инвойса, т.е с этого дня будет учитываться платеж
    /// </summary>
    [Column("date_start_invoice", TypeName = "timestamp without time zone")]
    public DateTime? DateStartInvoice { get; set; }

    /// <summary>
    /// сумма баланса, если сумма в минусе то долг.
    /// Сумма указывается в тыйынах
    /// </summary>
    [Column("balance")]
    public decimal? Balance { get; set; }

    [Column("pay_code", TypeName = "character varying")]
    public string? PayCode { get; set; }

    [Column("client", TypeName = "character varying")]
    public string? Client { get; set; }

    /// <summary>
    /// фиксированная сумма платежа, если не указан или 0 то сумма для платежа любая сумма
    /// </summary>
    [Column("fixed_summ", TypeName = "character varying")]
    public string? FixedSumm { get; set; }

    /// <summary>
    /// автопролонгация, если указан true - то при оплате за этот инвойс автоматом создается след запись в табилице графика платежей
    /// </summary>
    [Column("auto_prolongation")]
    public bool? AutoProlongation { get; set; }

    /// <summary>
    /// Дата начала следующего периода инвойса, если автопролонгация или инвойс установлен на несколько периодов
    /// </summary>
    [Column("next_start_invoice", TypeName = "timestamp without time zone")]
    public DateTime? NextStartInvoice { get; set; }

    [Column("name_invoice", TypeName = "character varying")]
    public string? NameInvoice { get; set; }

    [ForeignKey("Client")]
    [InverseProperty("Invoices")]
    public virtual OrganizationClient? ClientNavigation { get; set; }

    [InverseProperty("InvoiceNavigation")]
    public virtual ICollection<InvoicePayment> InvoicePayments { get; set; } = new List<InvoicePayment>();

    [InverseProperty("InvoiceNavigation")]
    public virtual ICollection<InvoiceService> InvoiceServices { get; set; } = new List<InvoiceService>();

    [InverseProperty("InvoiceNavigation")]
    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    [ForeignKey("UserCreater")]
    [InverseProperty("Invoices")]
    public virtual User? UserCreaterNavigation { get; set; }
}
