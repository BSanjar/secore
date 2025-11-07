using System;
using System.Collections.Generic;

namespace WebApplication1.Models.DBModels;

public partial class Organization
{
    public string Id { get; set; } = null!;

    public string? Name { get; set; }

    /// <summary>
    /// standart
    /// detsad
    /// school
    /// medclinic
    /// </summary>
    public string? Organizationtype { get; set; }

    public virtual ICollection<OrganizationClient> OrganizationClients { get; set; } = new List<OrganizationClient>();

    public virtual ICollection<OrganizationField> OrganizationFields { get; set; } = new List<OrganizationField>();

    public virtual ICollection<OrganizationService> OrganizationServices { get; set; } = new List<OrganizationService>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
