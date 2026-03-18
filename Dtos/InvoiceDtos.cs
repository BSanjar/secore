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
    /// <summary>Выбранный лицевой счёт (все счета создаются с ним).</summary>
    public string? PayCode { get; set; }
    /// <summary>Цена в сомах для одной позиции «название счёта» (режим отключенного выбора услуг).</summary>
    public decimal? ManualServicePriceSom { get; set; }
    /// <summary>Создать счёт с флагом «общий лицевой счёт» (новый PayCode, но Hassameaccount=true).</summary>
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

