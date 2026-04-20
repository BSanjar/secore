using System;
using System.Collections.Generic;

namespace TempScaffold.Models;

public partial class AppointmentMedicalTemplate
{
    public string Id { get; set; } = null!;

    public string OrganizationId { get; set; } = null!;

    public string TemplateType { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool? IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
