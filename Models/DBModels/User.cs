using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Models.DBModels;

[Table("users")]
public partial class User
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("name", TypeName = "character varying")]
    public string? Name { get; set; }

    [Column("email", TypeName = "character varying")]
    public string? Email { get; set; }

    [Column("phone", TypeName = "character varying")]
    public string? Phone { get; set; }

    [Column("password", TypeName = "character varying")]
    public string? Password { get; set; }

    [Column("isdeleted")]
    public int? Isdeleted { get; set; }

    [Column("organization", TypeName = "character varying")]
    public string? Organization { get; set; }

    /// <summary>
    /// user
    /// admin
    /// superadmin
    /// </summary>
    [Column("role", TypeName = "character varying")]
    public string? Role { get; set; }

    [InverseProperty("UserCreaterNavigation")]
    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    [InverseProperty("UserCreaterNavigation")]
    public virtual ICollection<OrganizationClient> OrganizationClients { get; set; } = new List<OrganizationClient>();

    [ForeignKey("Organization")]
    [InverseProperty("Users")]
    public virtual Organization? OrganizationNavigation { get; set; }

    [InverseProperty("UserNavigation")]
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
