using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models.DBModels;

[Table("appointment_services")]
public partial class AppointmentService
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("appointment_id", TypeName = "character varying")]
    public string AppointmentId { get; set; } = null!;

    [Column("organization_service_id", TypeName = "character varying")]
    public string? OrganizationServiceId { get; set; }

    [Column("service_name", TypeName = "character varying")]
    public string? ServiceName { get; set; }

    [Column("price_tyiyn", TypeName = "numeric(18,2)")]
    public decimal? PriceTyiyn { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [ForeignKey(nameof(AppointmentId))]
    [InverseProperty(nameof(DBModels.Appointment.AppointmentServices))]
    public virtual Appointment Appointment { get; set; } = null!;

    [ForeignKey(nameof(OrganizationServiceId))]
    [InverseProperty(nameof(DBModels.OrganizationService.AppointmentServices))]
    public virtual OrganizationService? OrganizationService { get; set; }
}
