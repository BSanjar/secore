using System;
using System.Collections.Generic;

namespace WebApplication1.Models.DBModels;

public partial class Invoice
{
    public string Id { get; set; } = null!;

    public DateTime? DateCreated { get; set; }

    public string? UserCreater { get; set; }

    /// <summary>
    /// actual
    /// suspended
    /// closed
    /// </summary>
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
    public string? Periodicity { get; set; }

    /// <summary>
    /// Дата начал инвойса, т.е с этого дня будет учитываться платеж
    /// </summary>
    public DateTime? DateStartInvoice { get; set; }

    /// <summary>
    /// сумма баланса, если сумма в минусе то долг.
    /// Сумма указывается в тыйынах
    /// </summary>
    public decimal? Balance { get; set; }

    public string? PayCode { get; set; }

    public string? Client { get; set; }

    /// <summary>
    /// фиксированная сумма платежа, если не указан или 0 то сумма для платежа любая сумма
    /// </summary>
    public string? FixedSumm { get; set; }

    /// <summary>
    /// автопролонгация, если указан true - то при оплате за этот инвойс автоматом создается след запись в табилице графика платежей
    /// </summary>
    public bool? AutoProlongation { get; set; }

    /// <summary>
    /// Дата начала следующего периода инвойса, если автопролонгация или инвойс установлен на несколько периодов
    /// </summary>
    public DateTime? NextStartInvoice { get; set; }

    public string? NameInvoice { get; set; }

    public virtual OrganizationClient? ClientNavigation { get; set; }

    public virtual ICollection<InvoicePayment> InvoicePayments { get; set; } = new List<InvoicePayment>();

    public virtual ICollection<InvoiceService> InvoiceServices { get; set; } = new List<InvoiceService>();

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public virtual User? UserCreaterNavigation { get; set; }
}
