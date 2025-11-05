using System;
using System.Collections.Generic;

namespace WebApplication1.Models.DBModels;

public partial class OrganizationClient
{
    public string Id { get; set; } = null!;

    public string? Organization { get; set; }

    public string? ClientName { get; set; }

    /// <summary>
    /// fiz\jur
    /// </summary>
    public string? ClinetType { get; set; }

    public string? ClientInn { get; set; }

    public string? ClientPhone { get; set; }

    public string? ClientAdres { get; set; }

    public string? ClientEmail { get; set; }

    /// <summary>
    /// баланс в тыйынах
    /// </summary>
    public decimal? ClientBalance { get; set; }

    /// <summary>
    /// 0\1
    /// </summary>
    public int? ClientStatus { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    /// <summary>
    /// тут заполненные доп поля клиента.
    /// </summary>
    public string? ClientFields { get; set; }

    public string? UserCreater { get; set; }

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual User? UserCreaterNavigation { get; set; }
}
