using System;
using System.Collections.Generic;

namespace WebApplication1.Models.DBModels;

public partial class OrganizationField
{
    public string Id { get; set; } = null!;

    public string? FieldName { get; set; }

    /// <summary>
    /// int
    /// string
    /// money
    /// selected
    /// datetime
    /// </summary>
    public string? FieldType { get; set; }

    /// <summary>
    /// варианты для выбора чз - ;
    /// </summary>
    public string? FieldSelectValues { get; set; }

    public int? Isdeleted { get; set; }

    public string? Organization { get; set; }

    public virtual Organization? OrganizationNavigation { get; set; }
}
