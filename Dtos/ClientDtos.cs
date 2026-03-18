using System;
using System.Collections.Generic;

namespace WebApplication1.Dtos;

/// <summary>
/// DTO для создания клиента (в Area Detsad — «ребёнок»).
/// </summary>
public class CreateChildRequest
{
    public string? ClientName { get; set; }
    public string? ClientInn { get; set; }
    public string? OrgClientGroupId { get; set; }
    public string? ClientPhone { get; set; }
    public string? ClientAdres { get; set; }
    public string? ClientEmail { get; set; }
    public string? ClientWa { get; set; }
    public string? ClientTg { get; set; }
    /// <summary>Ключ — Id поля (OrganizationField), значение — введённое значение</summary>
    public Dictionary<string, string>? AdditionalFields { get; set; }
}

/// <summary>
/// DTO для обновления клиента (в Area Detsad — «ребёнок»).
/// </summary>
public class UpdateChildRequest
{
    public string ClientId { get; set; } = null!;
    public string? ClientName { get; set; }
    public string? ClientInn { get; set; }
    public string? OrgClientGroupId { get; set; }
    public string? ClientPhone { get; set; }
    public string? ClientAdres { get; set; }
    public string? ClientEmail { get; set; }
    public string? ClientWa { get; set; }
    public string? ClientTg { get; set; }
    public int? ClientStatus { get; set; }
    public Dictionary<string, string>? AdditionalFields { get; set; }
}

public class CreateClientInput
{
    public string? ClientName { get; set; }
    public string? ClientInn { get; set; }
    public string? OrgClientGroupId { get; set; }
    public string? ClientPhone { get; set; }
    public string? ClientAddress { get; set; }
    public string? ClientEmail { get; set; }
    public string? ClientWa { get; set; }
    public string? ClientTg { get; set; }
    public Dictionary<string, string>? AdditionalFields { get; set; }
}

public class UpdateClientInput
{
    public string ClientId { get; set; } = null!;
    public string? ClientName { get; set; }
    public string? ClientInn { get; set; }
    public string? OrgClientGroupId { get; set; }
    public string? ClientPhone { get; set; }
    public string? ClientAddress { get; set; }
    public string? ClientEmail { get; set; }
    public string? ClientWa { get; set; }
    public string? ClientTg { get; set; }
    public int? ClientStatus { get; set; }
    public Dictionary<string, string>? AdditionalFields { get; set; }
}

public class ClientInfoDto
{
    public string? Id { get; set; }
    public string? ClientName { get; set; }
    public string? ClientPhone { get; set; }
    public string? ClientEmail { get; set; }
    public string? ClientAddress { get; set; }
    public string? ClientInn { get; set; }
    public decimal? ClientBalance { get; set; }
    public int? ClientStatus { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public string? ClientLogo { get; set; }
    public string? OrgClientGroupId { get; set; }
    public string? ClientWa { get; set; }
    public string? ClientTg { get; set; }
    public List<ClientAdditionalFieldDto> AdditionalFields { get; set; } = new();
}

public class ClientAdditionalFieldDto
{
    public string? FieldId { get; set; }
    public string? FieldName { get; set; }
    public string? FieldType { get; set; }
    public string? Value { get; set; }
}

