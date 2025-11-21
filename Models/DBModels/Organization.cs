using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Models.DBModels;

[Table("organization")]
public partial class Organization
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("name", TypeName = "character varying")]
    public string? Name { get; set; }

    /// <summary>
    /// standart
    /// detsad
    /// school
    /// medclinic
    /// </summary>
    [Column("organizationtype", TypeName = "character varying")]
    public string? Organizationtype { get; set; }

    [InverseProperty("OrganizationNavigation")]
    public virtual ICollection<OrganizationClient> OrganizationClients { get; set; } = new List<OrganizationClient>();

    [InverseProperty("OrganizationNavigation")]
    public virtual ICollection<OrganizationField> OrganizationFields { get; set; } = new List<OrganizationField>();

    [InverseProperty("OrganizationNavigation")]
    public virtual ICollection<OrganizationService> OrganizationServices { get; set; } = new List<OrganizationService>();

    [InverseProperty("OrganizationNavigation")]
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
