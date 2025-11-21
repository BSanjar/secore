using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Models.DBModels;

[Table("organization_clients_additional_fields")]
public partial class OrganizationClientsAdditionalField
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    /// <summary>
    /// ссылка на organization_fields
    /// </summary>
    [Column("field", TypeName = "character varying")]
    public string? Field { get; set; }

    /// <summary>
    /// значение переменной\поля
    /// </summary>
    [Column("value", TypeName = "character varying")]
    public string? Value { get; set; }

    /// <summary>
    /// ссылка на клиента
    /// </summary>
    [Column("organization_client", TypeName = "character varying")]
    public string? OrganizationClient { get; set; }

    [ForeignKey("Field")]
    [InverseProperty("OrganizationClientsAdditionalFields")]
    public virtual OrganizationField? FieldNavigation { get; set; }

    [ForeignKey("OrganizationClient")]
    [InverseProperty("OrganizationClientsAdditionalFields")]
    public virtual OrganizationClient? OrganizationClientNavigation { get; set; }
}
