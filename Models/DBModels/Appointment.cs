using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.DBModels;

[Table("appointments")]
public partial class Appointment
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("organization_id", TypeName = "character varying")]
    public string OrganizationId { get; set; } = null!;

    [Column("patient_id", TypeName = "character varying")]
    public string? PatientId { get; set; }

    [Column("doctor_id", TypeName = "character varying")]
    public string? DoctorId { get; set; }

    [Column("title", TypeName = "character varying")]
    public string? Title { get; set; }

    [Column("phone", TypeName = "character varying")]
    public string? Phone { get; set; }

    [Column("email", TypeName = "character varying")]
    public string? Email { get; set; }

    [Column("notes", TypeName = "text")]
    public string? Notes { get; set; }

    [Column("referral_source", TypeName = "character varying")]
    public string? ReferralSource { get; set; }

    [Column("payment_type", TypeName = "character varying")]
    public string? PaymentType { get; set; }

    [Column("appointment_status", TypeName = "character varying")]
    public string? AppointmentStatus { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("starts_at", TypeName = "timestamp without time zone")]
    public DateTime StartsAt { get; set; }

    [Column("ends_at", TypeName = "timestamp without time zone")]
    public DateTime EndsAt { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp without time zone")]
    public DateTime UpdatedAt { get; set; }

    [Column("created_by", TypeName = "character varying")]
    public string? CreatedBy { get; set; }

    [ForeignKey(nameof(OrganizationId))]
    [InverseProperty("Appointments")]
    public virtual Organization Organization { get; set; } = null!;

    [ForeignKey(nameof(PatientId))]
    [InverseProperty("Appointments")]
    public virtual OrganizationClient? Patient { get; set; }

    [ForeignKey(nameof(DoctorId))]
    [InverseProperty("Appointments")]
    public virtual User? Doctor { get; set; }

    [InverseProperty("Appointment")]
    public virtual ICollection<AppointmentService> AppointmentServices { get; set; } = new List<AppointmentService>();
}
