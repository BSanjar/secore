using System;
using System.Collections.Generic;

namespace WebApplication1.Models.DBModels;

/// <summary>
/// Группа клиентов организации. Может иметь родительскую группу (иерархия) и дочерние группы.
/// У организации может быть несколько групп, у группы — одна организация.
/// </summary>
public partial class OrgClientGroup
{
    public string Id { get; set; } = null!;

    /// <summary>
    /// Название группы
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Родительская группа (null для корневых групп)
    /// </summary>
    public string? ParentGroupId { get; set; }

    /// <summary>
    /// Организация, к которой принадлежит группа
    /// </summary>
    public string OrganizationId { get; set; } = null!;

    /// <summary>
    /// Признак удаления: 0 — активна, 1 — удалена (мягкое удаление)
    /// </summary>
    public int IsDeleted { get; set; }

    /// <summary>
    /// Логотип группы (путь к файлу или base64)
    /// </summary>
    public string? Logo { get; set; }

    /// <summary>
    /// Дата и время создания записи
    /// </summary>
    public DateTime? CreatedDate { get; set; }

    public virtual OrgClientGroup? ParentGroup { get; set; }

    public virtual ICollection<OrgClientGroup> ChildGroups { get; set; } = new List<OrgClientGroup>();

    public virtual Organization Organization { get; set; } = null!;

    public virtual ICollection<OrganizationClient> OrganizationClients { get; set; } = new List<OrganizationClient>();
}
