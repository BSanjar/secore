using System;
using System.Collections.Generic;

namespace TempScaffold.Models;

public partial class InvoiceQr
{
    public string Id { get; set; } = null!;

    public string InvoiceId { get; set; } = null!;

    /// <summary>
    /// active or disabled , ТОЛЬКО ЭТИ 2 ПАРАМЕТРА
    /// </summary>
    public string Status { get; set; } = null!;

    public string? QrLink { get; set; }

    public string? QrCodeBase64 { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DisabledAt { get; set; }

    public string? Transaction { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;
}
