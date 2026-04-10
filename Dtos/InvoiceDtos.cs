using System;
using System.Collections.Generic;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Dtos;

public class CreateInvoicesRequest
{
    public string? NameInvoice { get; set; }
    public DateTime? DateStartInvoice { get; set; }
    public DateTime? DateEndInvoice { get; set; }
    public string? Periodicity { get; set; }
    public bool AutoProlongation { get; set; }
    public bool UseCurrentDateTime { get; set; } = true;
    public List<string> ClientIds { get; set; } = new();
    public List<CreateInvoiceServiceItem> ServiceItems { get; set; } = new();
    /// <summary>Выбранный лицевой счёт, который будет использоваться для всех создаваемых счетов.</summary>
    public string? PayCode { get; set; }
    /// <summary>Цена в сомах для одной позиции счёта, когда услуги не выбираются явно.</summary>
    public decimal? ManualServicePriceSom { get; set; }
    /// <summary>Создать счёт с флагом общего лицевого счёта.</summary>
    public bool Hassameaccount { get; set; }
}

public class CreateInvoiceServiceItem
{
    public string? ServiceId { get; set; }
    public int? Qty { get; set; }
}

/// <summary>Входные параметры для OperationsByInvoices.CreateInvoicesAsync.</summary>
public class CreateInvoicesInput
{
    public string OrganizationId { get; set; } = "";
    public string UserId { get; set; } = "";
    public List<OrganizationClient> Clients { get; set; } = new();
    public string? NameInvoice { get; set; }
    public DateTime? DateStartInvoice { get; set; }
    public DateTime? DateEndInvoice { get; set; }
    public string? Periodicity { get; set; }
    public bool AutoProlongation { get; set; }
    public bool UseCurrentDateTime { get; set; } = true;
    public bool UseManualService { get; set; }
    public decimal? ManualServicePriceSom { get; set; }
    public List<CreateInvoiceServiceItemInput>? ServiceItems { get; set; }
    public Dictionary<string, OrganizationService>? OrgServices { get; set; }
    public string? PayCode { get; set; }
    public bool Hassameaccount { get; set; }
}

public class CreateInvoiceServiceItemInput
{
    public string? ServiceId { get; set; }
    public int? Qty { get; set; }
}

/// <summary>Входные параметры для создания разового счёта.</summary>
public class CreateOneTimeInvoiceInput
{
    public string OrganizationId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public decimal FixedSumm { get; set; }
    public string? InvoiceName { get; set; }
    public string? PayCode { get; set; }
    public DateTime? DateStartInvoice { get; set; }
    public DateTime? DateEndInvoice { get; set; }
    public decimal? Balance { get; set; }
    public bool Hassameaccount { get; set; }
    public bool CreateInvoicePayment { get; set; } = true;
    public DateTime? PaymentDateFrom { get; set; }
    public DateTime? PaymentDateTo { get; set; }
    public string? PaymentPeriodValue { get; set; }
    public decimal? PaymentSumm { get; set; }
    public List<CreateOneTimePaymentServiceLineInput> ServiceLines { get; set; } = new();
}

public class CreateOneTimePaymentServiceLineInput
{
    public string? OrganizationServiceId { get; set; }
    public decimal ServiceSumm { get; set; }
}

/// <summary>Результат создания разового счёта.</summary>
public class CreateOneTimeInvoiceResult
{
    public string InvoiceId { get; set; } = "";
    public string PayCode { get; set; } = "";
}
