using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Models.DBModels;

[Keyless]
[Table("history")]
public partial class History
{
    [Column("id", TypeName = "character varying")]
    public string Id { get; set; } = null!;

    /// <summary>
    /// user_edited
    /// invoice_edited
    /// </summary>
    [Column("type_history", TypeName = "character varying")]
    public string? TypeHistory { get; set; }

    /// <summary>
    /// Пользователь который совершил действие
    /// </summary>
    [Column("user_editor", TypeName = "character varying")]
    public string? UserEditor { get; set; }

    /// <summary>
    /// если действие по инвойсу то id инвойса
    /// </summary>
    [Column("invoice", TypeName = "character varying")]
    public string? Invoice { get; set; }
}
