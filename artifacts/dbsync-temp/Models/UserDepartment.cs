using System;
using System.Collections.Generic;

namespace TempScaffold.Models;

public partial class UserDepartment
{
    public string Id { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string DepartmentId { get; set; } = null!;

    public bool IsPrimary { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Department Department { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
