using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Models.DBModels;

[Table("permissions")]
public partial class Permission
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("name", TypeName = "character varying")]
    public string? Name { get; set; }

    [Column("code", TypeName = "character varying")]
    public string? Code { get; set; }

    [Column("description", TypeName = "character varying")]
    public string? Description { get; set; }

    [Column("category", TypeName = "character varying")]
    public string? Category { get; set; }

    /// <summary>
    /// standart, detsad, school, medclinic или null для общих прав
    /// </summary>
    [Column("area", TypeName = "character varying")]
    public string? Area { get; set; }

    [Column("isdeleted")]
    public int? Isdeleted { get; set; }

    [InverseProperty("PermissionNavigation")]
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

