using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.DBModels;

[Table("user_departments")]
public partial class UserDepartment
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("user_id", TypeName = "character varying")]
    public string UserId { get; set; } = null!;

    [Column("department_id", TypeName = "character varying")]
    public string DepartmentId { get; set; } = null!;

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey(nameof(UserId))]
    [InverseProperty("UserDepartments")]
    public virtual User User { get; set; } = null!;

    [ForeignKey(nameof(DepartmentId))]
    [InverseProperty("UserDepartments")]
    public virtual Department Department { get; set; } = null!;
}
