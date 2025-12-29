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

    /// <summary>
    /// если true - то организации могут создавать счета к оплате с одинаковыми л\с
    /// </summary>
    public bool AllowedHassameaccount { get; set; }

    /// <summary>
    /// Количество дней за которую будет начинатся отправка уведомлений по оплате на организацию
    /// </summary>
    public int Paymentreminderdaysbefore { get; set; }

    /// <summary>
    /// Логин для API
    /// </summary>
    public string? ApiLogin { get; set; }

    public string? ApiPassword { get; set; }

    public virtual ICollection<OrganizationClient> OrganizationClients { get; set; } = new List<OrganizationClient>();

    public virtual ICollection<OrganizationField> OrganizationFields { get; set; } = new List<OrganizationField>();

    public virtual ICollection<OrganizationService> OrganizationServices { get; set; } = new List<OrganizationService>();

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
