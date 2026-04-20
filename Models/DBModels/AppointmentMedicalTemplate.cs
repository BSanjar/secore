using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.DBModels;

[Table("appointment_medical_templates")]
public class AppointmentMedicalTemplate
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("organization_id", TypeName = "character varying")]
    public string OrganizationId { get; set; } = null!;

    [Column("template_type", TypeName = "character varying")]
    public string TemplateType { get; set; } = null!;

    [Column("title", TypeName = "character varying")]
    public string Title { get; set; } = null!;

    [Column("content", TypeName = "text")]
    public string Content { get; set; } = null!;

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp without time zone")]
    public DateTime UpdatedAt { get; set; }
}
