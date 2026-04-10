using System;
using System.Collections.Generic;

namespace WebApplication1.Models.DBModels;

public partial class User
{
    public string Id { get; set; } = null!;

    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Password { get; set; }

    public int? Isdeleted { get; set; }

    public string? Organization { get; set; }

    /// <summary>
    /// user
    /// admin
    /// superadmin
    /// </summary>
    public string? Role { get; set; }

    public string? GoogleId { get; set; }

    public string? GoogleEmail { get; set; }

    public bool? EmailConfirmed { get; set; }

    public bool? TwoFactorEnabled { get; set; }

    public string? TwoFactorType { get; set; }

    public string? TwoFactorSecret { get; set; }

    public string? TwoFactorCode { get; set; }

    public DateTime? TwoFactorCodeExpire { get; set; }

    public int? AccessFailedCount { get; set; }

    public DateTime? LockoutEnd { get; set; }

    public DateTime? LastLogin { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<UserWorkSchedule> UserWorkSchedules { get; set; } = new List<UserWorkSchedule>();

    public virtual ICollection<UserWorkScheduleOverride> UserWorkScheduleOverrides { get; set; } = new List<UserWorkScheduleOverride>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<OrganizationClient> OrganizationClients { get; set; } = new List<OrganizationClient>();

    public virtual ICollection<UserDepartment> UserDepartments { get; set; } = new List<UserDepartment>();

    public virtual ICollection<UserSpecialization> UserSpecializations { get; set; } = new List<UserSpecialization>();

    public virtual Organization? OrganizationNavigation { get; set; }

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
