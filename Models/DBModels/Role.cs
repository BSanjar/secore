using System;
using System.Collections.Generic;

namespace WebApplication1.Models.DBModels;

public partial class Role
{
    public string Id { get; set; } = null!;

    public string? Name { get; set; }

    public int? Isdeleted { get; set; }

    /// <summary>
    /// права пользователей
    /// </summary>
    public string? Rights { get; set; }

    /// <summary>
    /// перечисляется id сервисов чз ;
    /// </summary>
    public string? AvilableServices { get; set; }

    public virtual ICollection<UserRight> UserRights { get; set; } = new List<UserRight>();
}
