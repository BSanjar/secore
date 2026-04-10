using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.DBModels;

[Table("user_work_schedule_overrides")]
public partial class UserWorkScheduleOverride
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("organization_id", TypeName = "character varying")]
    public string OrganizationId { get; set; } = null!;

    [Column("user_id", TypeName = "character varying")]
    public string UserId { get; set; } = null!;

    [Column("work_date", TypeName = "date")]
    public DateTime WorkDate { get; set; }

    [Column("is_working")]
    public bool IsWorking { get; set; }

    [Column("start_time", TypeName = "time without time zone")]
    public TimeSpan? StartTime { get; set; }

    [Column("end_time", TypeName = "time without time zone")]
    public TimeSpan? EndTime { get; set; }

    [Column("comment", TypeName = "character varying")]
    public string? Comment { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp without time zone")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey(nameof(OrganizationId))]
    [InverseProperty("UserWorkScheduleOverrides")]
    public virtual Organization Organization { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    [InverseProperty("UserWorkScheduleOverrides")]
    public virtual User User { get; set; } = null!;
}
