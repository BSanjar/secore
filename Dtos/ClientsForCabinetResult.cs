using System.Collections.Generic;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Dtos;

/// <summary>
/// Результат загрузки данных для страницы «Дети» (список клиентов кабинета).
/// </summary>
public class ClientsForCabinetResult
{
    public List<OrganizationClient> ChildrenData { get; set; } = new();
    public Dictionary<string, decimal> ClientTotalBalanceByClientId { get; set; } = new();
    public Dictionary<string, string> FirstInvoiceIdByClientId { get; set; } = new();
    public List<OrganizationField> OrganizationFields { get; set; } = new();
    public List<OrgClientGroup> OrgClientGroups { get; set; } = new();
    public string? InvoicePayCodeMode { get; set; }
    public bool AllowedHassameaccount { get; set; }
}
