using System;
using System.Collections.Generic;

namespace TempScaffold.Models;

public partial class AppointmentService
{
    public string Id { get; set; } = null!;

    public string AppointmentId { get; set; } = null!;

    public string? OrganizationServiceId { get; set; }

    public string? ServiceName { get; set; }

    public decimal PriceTyiyn { get; set; }

    public int Quantity { get; set; }

    public virtual Appointment Appointment { get; set; } = null!;

    public virtual OrganizationService? OrganizationService { get; set; }
}
