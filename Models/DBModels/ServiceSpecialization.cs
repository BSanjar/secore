using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.DBModels;

[Table("service_specializations")]
public partial class ServiceSpecialization
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("organization_service_id", TypeName = "character varying")]
    public string OrganizationServiceId { get; set; } = null!;

    [Column("specialization_id", TypeName = "character varying")]
    public string SpecializationId { get; set; } = null!;

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey(nameof(OrganizationServiceId))]
    [InverseProperty("ServiceSpecializations")]
    public virtual OrganizationService OrganizationService { get; set; } = null!;

    [ForeignKey(nameof(SpecializationId))]
    [InverseProperty("ServiceSpecializations")]
    public virtual Specialization Specialization { get; set; } = null!;
}
