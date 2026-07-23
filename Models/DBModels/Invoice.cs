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
    /// Периодичность оплаты:
    /// daily - ежедневно
    /// weekly - еженедельно
    /// monthly - ежемесячно
    /// yearly - ежегодно
    /// если указывается конкретное число, значит каждые указанное число дней.
    /// т.е. если, к примеру, 30, то каждые 30 дней.
    /// oneTime - одноразовый и прием в любой момент
    /// any - прием в любой момент
    /// </summary>
    public string? Periodicity { get; set; }

    /// <summary>
    /// Дата начала инвойса, т.е. с этого дня будет учитываться платеж.
    /// </summary>
    public DateTime? DateStartInvoice { get; set; }

    /// <summary>
    /// Дата окончания инвойса.
    /// </summary>
    public DateTime? DateEndInvoice { get; set; }

    /// <summary>
    /// Сумма баланса, если сумма в минусе, то долг.
    /// Сумма указывается в тыйынах.
    /// </summary>
    public decimal? Balance { get; set; }

    public string? PayCode { get; set; }

    public string? Client { get; set; }

    /// <summary>
    /// Фиксированная сумма платежа. Если не указана или 0, то сумма для платежа может быть любой.
    /// </summary>
    public decimal? FixedSumm { get; set; }

    /// <summary>
    /// Автопролонгация. Если true, при оплате за этот инвойс автоматически создается следующая запись в графике платежей.
    /// </summary>
    public bool? AutoProlongation { get; set; }

    /// <summary>
    /// Дата начала следующего периода инвойса, если автопролонгация или инвойс установлен на несколько периодов.
    /// </summary>
    public DateTime? NextStartInvoice { get; set; }

    public string? NameInvoice { get; set; }

    /// <summary>
    /// Если true, то существует еще другой счет с таким же лицевым счетом и у них общий баланс.
    /// </summary>
    public bool Hassameaccount { get; set; }

    /// <summary>
    /// reuse_active - многоразовый, regenerate_per_period - с периодичностью.
    /// </summary>
    public string? QrMode { get; set; }

    /// <summary>Счёт создан из записи на приём (регистратура medclinic).</summary>
    public bool FromAppointments { get; set; }

    public virtual OrganizationClient? ClientNavigation { get; set; }

    public virtual ICollection<InvoicePayment> InvoicePayments { get; set; } = new List<InvoicePayment>();

    public virtual ICollection<InvoiceQr> InvoiceQrs { get; set; } = new List<InvoiceQr>();

    public virtual ICollection<InvoiceService> InvoiceServices { get; set; } = new List<InvoiceService>();

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public virtual User? UserCreaterNavigation { get; set; }
}
