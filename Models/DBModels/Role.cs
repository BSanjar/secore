using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Models.DBModels;

[Table("roles")]
public partial class Role
{
    [Key]
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    [Column("name", TypeName = "character varying")]
    public string? Name { get; set; }

    [Column("isdeleted")]
    public int? Isdeleted { get; set; }

    /// <summary>
    /// права пользователей
    /// </summary>
    [Column("rights", TypeName = "character varying")]
    public string? Rights { get; set; }

    /// <summary>
    /// перечисляется id сервисов чз ;
    /// </summary>
    [Column("avilable_services", TypeName = "character varying")]
    public string? AvilableServices { get; set; }

    [InverseProperty("RoleNavigation")]
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
