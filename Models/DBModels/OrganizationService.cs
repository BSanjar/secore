using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Models.DBModels;

[Table("organization_services")]
public partial class OrganizationService
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("name", TypeName = "character varying")]
    public string? Name { get; set; }

    [Column("organization", TypeName = "character varying")]
    public string? Organization { get; set; }

    /// <summary>
    /// если fixed_sum = 1, то тут будет значение фиксированной суммы
    /// </summary>
    [Column("service_summ")]
    public decimal? ServiceSumm { get; set; }

    /// <summary>
    /// если 1 то услуга с фиксированной суммой
    /// </summary>
    [Column("fixed_sum")]
    public int? FixedSum { get; set; }

    /// <summary>
    /// мин сумма в тыйынах
    /// </summary>
    [Column("min_summ")]
    public decimal? MinSumm { get; set; }

    /// <summary>
    /// макс сумма в тыйынах
    /// </summary>
    [Column("max_summ")]
    public decimal? MaxSumm { get; set; }

    [Column("isdeleted")]
    public int? Isdeleted { get; set; }

    [InverseProperty("ServiceNavigation")]
    public virtual ICollection<InvoiceService> InvoiceServices { get; set; } = new List<InvoiceService>();

    [ForeignKey("Organization")]
    [InverseProperty("OrganizationServices")]
    public virtual Organization? OrganizationNavigation { get; set; }
}
