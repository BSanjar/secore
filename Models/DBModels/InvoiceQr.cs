using System;

namespace WebApplication1.Models.DBModels;

public partial class InvoiceQr
{
    public string Id { get; set; } = null!;

    public string InvoiceId { get; set; } = null!;

    /// <summary>Лицевой счёт — один QR на pay_code.</summary>
    public string? PayCode { get; set; }

    public string? Transaction { get; set; }

    public string Status { get; set; } = null!;

    public string? QrLink { get; set; }

    public string? QrCodeBase64 { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DisabledAt { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;
}
