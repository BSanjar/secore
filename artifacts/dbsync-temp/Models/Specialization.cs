using System;
using System.Collections.Generic;

namespace TempScaffold.Models;

public partial class Specialization
{
    public string Id { get; set; } = null!;

    public string OrganizationId { get; set; } = null!;

    public string? DepartmentId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool? IsActive { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Department? Department { get; set; }

    public virtual Organization Organization { get; set; } = null!;

    public virtual ICollection<ServiceSpecialization> ServiceSpecializations { get; set; } = new List<ServiceSpecialization>();

    public virtual ICollection<UserSpecialization> UserSpecializations { get; set; } = new List<UserSpecialization>();
}
