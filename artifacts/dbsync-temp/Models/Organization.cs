using System;
using System.Collections.Generic;

namespace TempScaffold.Models;

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
    /// false — доступ заблокирован (истёк период подписки)
    /// </summary>
    public bool? IsActive { get; set; }

    public virtual ICollection<AgentCommission> AgentCommissions { get; set; } = new List<AgentCommission>();

    public virtual ICollection<AppointmentSetting> AppointmentSettings { get; set; } = new List<AppointmentSetting>();

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();

    public virtual ICollection<OrgClientGroup> OrgClientGroups { get; set; } = new List<OrgClientGroup>();

    public virtual ICollection<OrganizationClient> OrganizationClients { get; set; } = new List<OrganizationClient>();

    public virtual ICollection<OrganizationField> OrganizationFields { get; set; } = new List<OrganizationField>();

    public virtual ICollection<OrganizationService> OrganizationServices { get; set; } = new List<OrganizationService>();

    public virtual OrganizationSetting? OrganizationSetting { get; set; }

    public virtual OrganizationSubscription? OrganizationSubscription { get; set; }

    public virtual ICollection<OrganizationSubscriptionPayment> OrganizationSubscriptionPayments { get; set; } = new List<OrganizationSubscriptionPayment>();

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();

    public virtual ICollection<Specialization> Specializations { get; set; } = new List<Specialization>();

    public virtual ICollection<UserWorkScheduleOverride> UserWorkScheduleOverrides { get; set; } = new List<UserWorkScheduleOverride>();

    public virtual ICollection<UserWorkSchedule> UserWorkSchedules { get; set; } = new List<UserWorkSchedule>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
