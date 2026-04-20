using System;
using System.Collections.Generic;

namespace TempScaffold.Models;

public partial class ServiceSpecialization
{
    public string Id { get; set; } = null!;

    public string OrganizationServiceId { get; set; } = null!;

    public string SpecializationId { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual OrganizationService OrganizationService { get; set; } = null!;

    public virtual Specialization Specialization { get; set; } = null!;
}
