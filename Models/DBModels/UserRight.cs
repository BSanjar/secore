using System;
using System.Collections.Generic;

namespace WebApplication1.Models.DBModels;

public partial class UserRight
{
    public string Id { get; set; } = null!;

    public string? User { get; set; }

    public string? Role { get; set; }

    public int? Isdeleted { get; set; }

    public virtual Role? RoleNavigation { get; set; }

    public virtual User? UserNavigation { get; set; }
}
