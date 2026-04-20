using System;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.Dtos;

namespace WebApplication1.Services;

/// <summary>
/// Сервис работы с клиентами организации (OrganizationClient).
/// В Area Detsad клиент отображается как «ребёнок», для остальных организаций — как «клиент».
/// </summary>
public class ClientService
{
    private readonly AppDbContext _db;

    public ClientService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Создаёт клиента организации и при необходимости дополнительные поля. Возвращает Id созданного клиента.
    /// </summary>
    public async Task<string> CreateClientAsync(CreateClientInput input, string organizationId, string userId, CancellationToken cancellationToken = default)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var newClientId = Guid.NewGuid().ToString();

            var organizationClient = new OrganizationClient
            {
                Id = newClientId,
                Organization = organizationId,
                OrgClientGroupId = string.IsNullOrWhiteSpace(input.OrgClientGroupId) ? null : input.OrgClientGroupId,
                ClientName = input.ClientName,
                ClientType = "fiz",
                ClientInn = input.ClientInn,
                ClientPhone = input.ClientPhone,
                ClientAddress = input.ClientAddress,
                ClientEmail = input.ClientEmail,
                ClientWa = input.ClientWa,
                ClientTg = input.ClientTg,
                ClientBalance = 0,
                ClientStatus = 1,
                CreatedDate = DateTime.Now,
                UpdatedDate = DateTime.Now,
                UserCreater = userId
            };

            _db.OrganizationClients.Add(organizationClient);

            if (input.AdditionalFields != null && input.AdditionalFields.Count > 0)
            {
                var orgFieldIds = await _db.OrganizationFields
                    .Where(f => f.Organization == organizationId && (f.Isdeleted == null || f.Isdeleted == 0))
                    .Select(f => f.Id)
                    .ToListAsync(cancellationToken);

                foreach (var kv in input.AdditionalFields)
                {
                    if (string.IsNullOrWhiteSpace(kv.Value) || !orgFieldIds.Contains(kv.Key)) continue;
                    _db.OrganizationClientsAdditionalFields.Add(new OrganizationClientsAdditionalField
                    {
                        Id = Guid.NewGuid().ToString(),
                        OrganizationClient = newClientId,
                        Field = kv.Key,
                        Value = kv.Value.Trim()
                    });
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return newClientId;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Возвращает данные клиента и его дополнительных полей для отображения/редактирования. Null, если клиент не найден или не принадлежит организации.
    /// </summary>
    public async Task<ClientInfoDto?> GetClientInfoAsync(string clientId, string organizationId, CancellationToken cancellationToken = default)
    {
        var client = await _db.OrganizationClients
            .Include(c => c.OrganizationClientsAdditionalFields)
            .ThenInclude(af => af.FieldNavigation)
            .FirstOrDefaultAsync(c => c.Id == clientId && c.Organization == organizationId, cancellationToken);

        if (client == null)
            return null;

        var additionalFields = client.OrganizationClientsAdditionalFields
            .Where(af => af.FieldNavigation != null)
            .Select(af => new ClientAdditionalFieldDto
            {
                FieldId = af.Field,
                FieldName = af.FieldNavigation!.FieldName,
                FieldType = af.FieldNavigation.FieldType,
                Value = af.Value
            })
            .ToList();

        return new ClientInfoDto
        {
            Id = client.Id,
            ClientName = client.ClientName,
            ClientPhone = client.ClientPhone,
            ClientEmail = client.ClientEmail,
            ClientAddress = client.ClientAddress,
            ClientInn = client.ClientInn,
            ClientBalance = client.ClientBalance,
            ClientStatus = client.ClientStatus,
            CreatedDate = client.CreatedDate,
            UpdatedDate = client.UpdatedDate,
            ClientLogo = client.ClientLogo,
            OrgClientGroupId = client.OrgClientGroupId,
            ClientWa = client.ClientWa,
            ClientTg = client.ClientTg,
            AdditionalFields = additionalFields
        };
    }

    /// <summary>
    /// Обновляет данные клиента и его дополнительных полей. Возвращает true при успехе, false если клиент не найден.
    /// </summary>
    public async Task<bool> UpdateClientAsync(UpdateClientInput input, string organizationId, CancellationToken cancellationToken = default)
    {
        var client = await _db.OrganizationClients
            .Include(c => c.OrganizationClientsAdditionalFields)
            .FirstOrDefaultAsync(c => c.Id == input.ClientId && c.Organization == organizationId, cancellationToken);

        if (client == null)
            return false;

        client.ClientName = input.ClientName ?? client.ClientName;
        client.ClientInn = input.ClientInn ?? client.ClientInn;
        client.OrgClientGroupId = string.IsNullOrWhiteSpace(input.OrgClientGroupId) ? null : input.OrgClientGroupId;
        client.ClientPhone = input.ClientPhone ?? client.ClientPhone;
        client.ClientAddress = input.ClientAddress ?? client.ClientAddress;
        client.ClientEmail = input.ClientEmail ?? client.ClientEmail;
        client.ClientWa = input.ClientWa ?? client.ClientWa;
        client.ClientTg = input.ClientTg ?? client.ClientTg;
        client.UpdatedDate = DateTime.Now;
        client.ClientStatus = input.ClientStatus ?? client.ClientStatus ?? 1;

        if (input.AdditionalFields != null)
        {
            var existing = client.OrganizationClientsAdditionalFields.ToList();
            foreach (var af in existing)
                _db.OrganizationClientsAdditionalFields.Remove(af);

            var orgFieldIds = await _db.OrganizationFields
                .Where(f => f.Organization == organizationId && (f.Isdeleted == null || f.Isdeleted == 0))
                .Select(f => f.Id)
                .ToListAsync(cancellationToken);

            foreach (var kv in input.AdditionalFields)
            {
                if (string.IsNullOrWhiteSpace(kv.Key) || !orgFieldIds.Contains(kv.Key)) continue;
                _db.OrganizationClientsAdditionalFields.Add(new OrganizationClientsAdditionalField
                {
                    Id = Guid.NewGuid().ToString(),
                    OrganizationClient = client.Id,
                    Field = kv.Key,
                    Value = (kv.Value ?? "").Trim()
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Сохраняет путь к фото клиента (относительный URL) или очищает при <paramref name="relativeLogoUrl"/> = null.
    /// </summary>
    public async Task<bool> SetClientLogoAsync(string clientId, string organizationId, string? relativeLogoUrl, CancellationToken cancellationToken = default)
    {
        var client = await _db.OrganizationClients
            .FirstOrDefaultAsync(c => c.Id == clientId && c.Organization == organizationId, cancellationToken);

        if (client == null)
            return false;

        client.ClientLogo = relativeLogoUrl;
        client.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Загружает данные для страницы «Дети» (список клиентов кабинета): клиенты с фильтрами, балансы, первый счёт по клиенту, поля организации, группы, настройки.
    /// </summary>
    public async Task<ClientsForCabinetResult> GetClientsForCabinetAsync(
        string organizationId,
        string search = "",
        string statusFilter = "active",
        bool debtorsOnly = false,
        CancellationToken cancellationToken = default)
    {
        var childrenData = await _db.OrganizationClients
            .Include(c => c.OrganizationClientsAdditionalFields)
            .ThenInclude(af => af.FieldNavigation)
            .Where(c => c.Organization == organizationId)
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            childrenData = childrenData.Where(c =>
                (c.ClientName != null && c.ClientName.ToLower().Contains(searchLower)) ||
                (c.ClientPhone != null && c.ClientPhone.Contains(search)) ||
                (c.ClientEmail != null && c.ClientEmail.ToLower().Contains(searchLower)) ||
                (c.ClientAddress != null && c.ClientAddress.ToLower().Contains(searchLower)) ||
                (c.OrganizationClientsAdditionalFields.Any(af =>
                    af.Value != null && af.Value.ToLower().Contains(searchLower)))
            ).ToList();
        }

        if (statusFilter == "active")
            childrenData = childrenData.Where(c => c.ClientStatus == 1).ToList();
        else if (statusFilter == "inactive")
            childrenData = childrenData.Where(c => c.ClientStatus == 0).ToList();
        else if (statusFilter == "deleted")
            childrenData = childrenData.Where(c => c.ClientStatus == null || c.ClientStatus < 0).ToList();

        var clientIdsForBalance = childrenData.Select(c => c.Id).ToList();
        var totalBalanceByClient = await _db.Invoices
            .Where(i => i.Client != null && clientIdsForBalance.Contains(i.Client) && i.InvoiceStatus == "actual")
            .GroupBy(i => i.Client!)
            .Select(g => new { ClientId = g.Key, TotalBalance = g.Sum(i => i.Balance ?? 0m) })
            .ToDictionaryAsync(x => x.ClientId, x => x.TotalBalance, cancellationToken);

        if (debtorsOnly)
        {
            childrenData = childrenData.Where(c =>
                c.ClientStatus == 1 &&
                totalBalanceByClient.GetValueOrDefault(c.Id, 0m) < 0).ToList();
        }

        var organizationFields = await _db.OrganizationFields
            .Where(f => f.Organization == organizationId && (f.Isdeleted == null || f.Isdeleted == 0))
            .ToListAsync(cancellationToken);

        var orgClientGroups = await _db.OrgClientGroups
            .Where(g => g.OrganizationId == organizationId && g.IsDeleted == 0)
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);

        var childrenViewData = childrenData
            .OrderByDescending(x => x.CreatedDate)
            .ToList();

        var clientIds = childrenViewData.Select(c => c.Id).ToList();
        var firstInvoiceIdByClient = new Dictionary<string, string>();
        if (clientIds.Count > 0)
        {
            var invoicesForClients = await _db.Invoices
                .Where(i => i.Client != null && clientIds.Contains(i.Client))
                .OrderByDescending(i => i.DateCreated)
                .Select(i => new { i.Client, i.Id })
                .ToListAsync(cancellationToken);
            foreach (var g in invoicesForClients.GroupBy(i => i.Client!))
                firstInvoiceIdByClient[g.Key] = g.First().Id;
        }

        var orgSettings = await _db.OrganizationSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId, cancellationToken);
        var invoicePayCodeMode = !string.IsNullOrEmpty(orgSettings?.InvoicePayCodeMode)
            ? orgSettings.InvoicePayCodeMode
            : (orgSettings?.AllowedHassameaccount == true ? "both" : "new_only");

        return new ClientsForCabinetResult
        {
            ChildrenData = childrenViewData,
            ClientTotalBalanceByClientId = totalBalanceByClient,
            FirstInvoiceIdByClientId = firstInvoiceIdByClient,
            OrganizationFields = organizationFields,
            OrgClientGroups = orgClientGroups,
            InvoicePayCodeMode = invoicePayCodeMode,
            AllowedHassameaccount = orgSettings?.AllowedHassameaccount ?? false
        };
    }
}
