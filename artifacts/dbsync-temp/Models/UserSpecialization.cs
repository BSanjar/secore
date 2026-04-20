using System;
using System.Collections.Generic;

namespace TempScaffold.Models;

public partial class UserSpecialization
{
    public string Id { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string SpecializationId { get; set; } = null!;

    public bool IsPrimary { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Specialization Specialization { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
