using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Models.DBModels;

[Table("user_roles")]
public partial class UserRole
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("user", TypeName = "character varying")]
    public string? User { get; set; }

    [Column("role", TypeName = "character varying")]
    public string? Role { get; set; }

    [Column("isdeleted")]
    public int? Isdeleted { get; set; }

    [ForeignKey("Role")]
    [InverseProperty("UserRoles")]
    public virtual Role? RoleNavigation { get; set; }

    [ForeignKey("User")]
    [InverseProperty("UserRoles")]
    public virtual User? UserNavigation { get; set; }
}
