using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.DBModels;

[Table("appointment_settings")]
public class AppointmentSetting
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("organization_id", TypeName = "character varying")]
    public string OrganizationId { get; set; } = null!;

    [Column("user_id", TypeName = "character varying")]
    public string UserId { get; set; } = null!;

    [Column("appointment_duration_minutes")]
    public int AppointmentDurationMinutes { get; set; } = 30;

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp without time zone")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey(nameof(OrganizationId))]
    [InverseProperty(nameof(DBModels.Organization.AppointmentSettings))]
    public virtual Organization Organization { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    [InverseProperty(nameof(DBModels.User.AppointmentSettings))]
    public virtual User User { get; set; } = null!;
}
