using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Models.DBModels;

[Table("invoice_services")]
public partial class InvoiceService
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("invoice", TypeName = "character varying")]
    public string? Invoice { get; set; }

    [Column("service", TypeName = "character varying")]
    public string? Service { get; set; }

    /// <summary>
    /// стоимость сервиса
    /// </summary>
    [Column("service_summ")]
    public decimal? ServiceSumm { get; set; }

    [ForeignKey("Invoice")]
    [InverseProperty("InvoiceServices")]
    public virtual Invoice? InvoiceNavigation { get; set; }

    [ForeignKey("Service")]
    [InverseProperty("InvoiceServices")]
    public virtual OrganizationService? ServiceNavigation { get; set; }
}
