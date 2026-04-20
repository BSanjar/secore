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
    /// medclinic
    /// simple
    /// </summary>
    public string? Organizationtype { get; set; }

    /// <summary>false — доступ в систему заблокирован (например, истёк период подписки).</summary>
    public bool IsActive { get; set; } = true;

    public virtual ICollection<OrganizationClient> OrganizationClients { get; set; } = new List<OrganizationClient>();

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<UserWorkSchedule> UserWorkSchedules { get; set; } = new List<UserWorkSchedule>();

    public virtual ICollection<UserWorkScheduleOverride> UserWorkScheduleOverrides { get; set; } = new List<UserWorkScheduleOverride>();
    public virtual ICollection<AppointmentSetting> AppointmentSettings { get; set; } = new List<AppointmentSetting>();

    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();

    public virtual ICollection<OrgClientGroup> OrgClientGroups { get; set; } = new List<OrgClientGroup>();

    public virtual ICollection<OrganizationField> OrganizationFields { get; set; } = new List<OrganizationField>();

    public virtual ICollection<OrganizationService> OrganizationServices { get; set; } = new List<OrganizationService>();

    public virtual ICollection<Specialization> Specializations { get; set; } = new List<Specialization>();


    public virtual ICollection<User> Users { get; set; } = new List<User>();

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();

    public virtual OrganizationSettings? Settings { get; set; }
    public virtual ICollection<AgentCommission> AgentCommissions { get; set; } = new List<AgentCommission>();
    public virtual OrganizationSubscription? Subscription { get; set; }
    public virtual ICollection<OrganizationSubscriptionPayment> SubscriptionPayments { get; set; } = new List<OrganizationSubscriptionPayment>();
}
