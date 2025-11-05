using System;
using System.Collections.Generic;

namespace WebApplication1.Models.DBModels;

public partial class Payment
{
    public string Id { get; set; } = null!;

    /// <summary>
    /// код оплаты, лицевой счет, счет на оплату итд. формат будет так:
    ///  {6-значный код}+{-}+{id организации}
    /// </summary>
    public string? PaymentCode { get; set; }

    public string? Client { get; set; }

    public string? UserCreater { get; set; }

    public DateTime? DateCreate { get; set; }

    /// <summary>
    /// сумма платежа в тыйынах
    /// </summary>
    public decimal? PaymentSumm { get; set; }

    /// <summary>
    /// сумма комиссии с клиента в тыйынах 
    /// </summary>
    public decimal? PaymentFee { get; set; }

    /// <summary>
    /// сумма к оплате вместе с комиссией
    /// </summary>
    public string? PaymentTotalSumm { get; set; }

    /// <summary>
    /// created - создан но еще транзакций нету
    /// in_process - если данный платеж долгосрочный и по данному платежу еще будут проводится транзакции
    /// closed - платеж закрыт
    /// annulated - аннулирован
    /// </summary>
    public string? PaymentStatus { get; set; }

    /// <summary>
    /// onetime - одноразовый платеж
    /// anytime - многоразовый платеж
    /// </summary>
    public string? PaymentType { get; set; }

    /// <summary>
    /// если 1 то платеж с фиксированной суммой
    /// если 0 то платеж без суммы, клиент может оплачивать любую сумму
    /// </summary>
    public int? FixedSumm { get; set; }

    public virtual OrganizationClient? ClientNavigation { get; set; }

    public virtual User? UserCreaterNavigation { get; set; }
}
