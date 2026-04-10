using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.DBModels;

[Table("departments")]
public partial class Department
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("organization_id", TypeName = "character varying")]
    public string OrganizationId { get; set; } = null!;

    [Column("name", TypeName = "character varying")]
    public string Name { get; set; } = null!;

    [Column("code", TypeName = "character varying")]
    public string? Code { get; set; }

    [Column("description", TypeName = "text")]
    public string? Description { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp without time zone")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey(nameof(OrganizationId))]
    [InverseProperty("Departments")]
    public virtual Organization Organization { get; set; } = null!;

    [InverseProperty("Department")]
    public virtual ICollection<Specialization> Specializations { get; set; } = new List<Specialization>();

    [InverseProperty("Department")]
    public virtual ICollection<UserDepartment> UserDepartments { get; set; } = new List<UserDepartment>();
}
