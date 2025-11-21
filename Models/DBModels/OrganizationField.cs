using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Models.DBModels;

[Table("organization_fields")]
public partial class OrganizationField
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("field_name", TypeName = "character varying")]
    public string? FieldName { get; set; }

    /// <summary>
    /// int
    /// string
    /// money
    /// selected
    /// datetime
    /// </summary>
    [Column("field_type", TypeName = "character varying")]
    public string? FieldType { get; set; }

    /// <summary>
    /// варианты для выбора чз - ;
    /// </summary>
    [Column("field_select_values", TypeName = "character varying")]
    public string? FieldSelectValues { get; set; }

    [Column("isdeleted")]
    public int? Isdeleted { get; set; }

    [Column("organization", TypeName = "character varying")]
    public string? Organization { get; set; }

    [Column("filterbyfield")]
    public bool? Filterbyfield { get; set; }

    [InverseProperty("FieldNavigation")]
    public virtual ICollection<OrganizationClientsAdditionalField> OrganizationClientsAdditionalFields { get; set; } = new List<OrganizationClientsAdditionalField>();

    [ForeignKey("Organization")]
    [InverseProperty("OrganizationFields")]
    public virtual Organization? OrganizationNavigation { get; set; }
}
