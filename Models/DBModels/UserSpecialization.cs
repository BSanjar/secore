using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.DBModels;

[Table("user_specializations")]
public partial class UserSpecialization
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("user_id", TypeName = "character varying")]
    public string UserId { get; set; } = null!;

    [Column("specialization_id", TypeName = "character varying")]
    public string SpecializationId { get; set; } = null!;

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey(nameof(UserId))]
    [InverseProperty("UserSpecializations")]
    public virtual User User { get; set; } = null!;

    [ForeignKey(nameof(SpecializationId))]
    [InverseProperty("UserSpecializations")]
    public virtual Specialization Specialization { get; set; } = null!;
}
