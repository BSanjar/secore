using System;
using System.Collections.Generic;

namespace TempScaffold.Models;

public partial class Appointment
{
    public string Id { get; set; } = null!;

    public string OrganizationId { get; set; } = null!;

    public string? PatientId { get; set; }

    public string? DoctorId { get; set; }

    public string? Title { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Notes { get; set; }

    public string? ReferralSource { get; set; }

    public string? PaymentType { get; set; }

    public string? AppointmentStatus { get; set; }

    public bool? IsActive { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public virtual ICollection<AppointmentService> AppointmentServices { get; set; } = new List<AppointmentService>();

    public virtual User? Doctor { get; set; }

    public virtual Organization Organization { get; set; } = null!;

    public virtual OrganizationClient? Patient { get; set; }
}
