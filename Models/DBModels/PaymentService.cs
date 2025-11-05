using System;
using System.Collections.Generic;

namespace WebApplication1.Models.DBModels;

public partial class PaymentService
{
    public string Id { get; set; } = null!;

    public string? Payment { get; set; }

    public string? Service { get; set; }

    public virtual ICollection<PaymentService> InversePaymentNavigation { get; set; } = new List<PaymentService>();

    public virtual PaymentService? PaymentNavigation { get; set; }

    public virtual OrganizationService? ServiceNavigation { get; set; }
}
