using System;
using System.Collections.Generic;

namespace WebApplication1.Dtos;

public class UpdateInvoiceRequest
{
    public string? Id { get; set; }
    public string? Client { get; set; }
    public string? NameInvoice { get; set; }
    public string? PayCode { get; set; }
    public string? Periodicity { get; set; }
    public string? InvoiceStatus { get; set; }
    public DateTime? DateStartInvoice { get; set; }
    public DateTime? DateEndInvoice { get; set; }
    public bool AutoProlongation { get; set; }
    public DateTime? NextStartInvoice { get; set; }
    public bool Hassameaccount { get; set; }
    public decimal? ManualServicePriceSom { get; set; }
    public List<UpdateInvoiceServiceItemDto>? ServiceItems { get; set; }
}

public class UpdateInvoiceServiceItemDto
{
    public string? ServiceId { get; set; }
    public int? Qty { get; set; }
}

public class EditServiceItemDto
{
    public string? ServiceId { get; set; }
    public int? Qty { get; set; }
}

public class DeleteInvoiceRequest
{
    public string? InvoiceId { get; set; }
}
