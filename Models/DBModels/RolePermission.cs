using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Models.DBModels;

[Table("role_permissions")]
public partial class RolePermission
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("role", TypeName = "character varying")]
    public string? Role { get; set; }

    [Column("permission", TypeName = "character varying")]
    public string? Permission { get; set; }

    [Column("isdeleted")]
    public int? Isdeleted { get; set; }

    [ForeignKey("Permission")]
    [InverseProperty("RolePermissions")]
    public virtual Permission? PermissionNavigation { get; set; }

    [ForeignKey("Role")]
    [InverseProperty("RolePermissions")]
    public virtual Role? RoleNavigation { get; set; }
}

