using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.DBModels;

[Table("user_work_schedules")]
public partial class UserWorkSchedule
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("organization_id", TypeName = "character varying")]
    public string OrganizationId { get; set; } = null!;

    [Column("user_id", TypeName = "character varying")]
    public string UserId { get; set; } = null!;

    [Column("day_of_week")]
    public int DayOfWeek { get; set; }

    [Column("start_time", TypeName = "time without time zone")]
    public TimeSpan StartTime { get; set; }

    [Column("end_time", TypeName = "time without time zone")]
    public TimeSpan EndTime { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp without time zone")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey(nameof(OrganizationId))]
    [InverseProperty("UserWorkSchedules")]
    public virtual Organization Organization { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    [InverseProperty("UserWorkSchedules")]
    public virtual User User { get; set; } = null!;
}
