using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.DBModels;

[Table("specializations")]
public partial class Specialization
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("organization_id", TypeName = "character varying")]
    public string OrganizationId { get; set; } = null!;

    [Column("department_id", TypeName = "character varying")]
    public string? DepartmentId { get; set; }

    [Column("name", TypeName = "character varying")]
    public string Name { get; set; } = null!;

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
    [InverseProperty("Specializations")]
    public virtual Organization Organization { get; set; } = null!;

    [ForeignKey(nameof(DepartmentId))]
    [InverseProperty("Specializations")]
    public virtual Department? Department { get; set; }

    [InverseProperty("Specialization")]
    public virtual ICollection<UserSpecialization> UserSpecializations { get; set; } = new List<UserSpecialization>();

    [InverseProperty("Specialization")]
    public virtual ICollection<ServiceSpecialization> ServiceSpecializations { get; set; } = new List<ServiceSpecialization>();
}
