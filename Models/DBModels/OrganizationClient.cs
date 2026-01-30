using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Models.DBModels;

[Table("organization_clients")]
public partial class OrganizationClient
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("organization", TypeName = "character varying")]
    public string? Organization { get; set; }

    [Column("client_name", TypeName = "character varying")]
    public string? ClientName { get; set; }

    /// <summary>
    /// fiz\jur
    /// </summary>
    [Column("client_type", TypeName = "character varying")]
    public string? ClientType { get; set; }

    [Column("client_inn", TypeName = "character varying")]
    public string? ClientInn { get; set; }

    [Column("client_phone", TypeName = "character varying")]
    public string? ClientPhone { get; set; }

    [Column("client_address", TypeName = "character varying")]
    public string? ClientAddress { get; set; }

    [Column("client_email", TypeName = "character varying")]
    public string? ClientEmail { get; set; }

    /// <summary>
    /// баланс в тыйынах
    /// </summary>
    [Column("client_balance")]
    public decimal? ClientBalance { get; set; }

    /// <summary>
    /// 0\1
    /// </summary>  
    [Column("client_status")]
    public int? ClientStatus { get; set; }

    [Column("created_date", TypeName = "timestamp without time zone")]
    public DateTime? CreatedDate { get; set; }

    [Column("updated_date", TypeName = "timestamp without time zone")]
    public DateTime? UpdatedDate { get; set; }

    [Column("user_creater", TypeName = "character varying")]
    public string? UserCreater { get; set; }

    [Column("client_logo", TypeName = "character varying")]
    public string? ClientLogo { get; set; }

    [Column("client_wa", TypeName = "character varying")]
    public string? ClientWa { get; set; }
    [Column("client_tg", TypeName = "character varying")]
    public string? ClientTg { get; set; }

    [Column("org_client_group_id", TypeName = "character varying")]
    public string? OrgClientGroupId { get; set; }

    [InverseProperty("ClientNavigation")]
    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    [InverseProperty("OrganizationClientNavigation")]
    public virtual ICollection<OrganizationClientsAdditionalField> OrganizationClientsAdditionalFields { get; set; } = new List<OrganizationClientsAdditionalField>();

    [InverseProperty("ClientNavigation")]
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    [ForeignKey("Organization")]
    [InverseProperty("OrganizationClients")]
    public virtual Organization? OrganizationNavigation { get; set; }

    [ForeignKey("UserCreater")]
    [InverseProperty("OrganizationClients")]
    public virtual User? UserCreaterNavigation { get; set; }

    [ForeignKey("OrgClientGroupId")]
    [InverseProperty("OrganizationClients")]
    public virtual OrgClientGroup? OrgClientGroup { get; set; }
}
