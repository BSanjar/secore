using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApplication1.Dtos;
using WebApplication1.Models.BaseModels;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Services;

public class InvoiceQrService
{
    public const string StatusActive = "active";
    public const string StatusDisabled = "disabled";
    public const string QrModeReuseActive = "reuse_active";
    public const string QrModeRegeneratePerPeriod = "regenerate_per_period";

    private readonly AppDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly AbQrOptions _options;

    public InvoiceQrService(AppDbContext db, HttpClient httpClient, IOptions<AbQrOptions> options)
    {
        _db = db;
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<InvoiceQr?> GetActiveQrAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        return await _db.InvoiceQrs
            .AsNoTracking()
            .Where(x => x.InvoiceId == invoiceId && x.Status == StatusActive)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<InvoiceQr?> DisableActiveQrAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        var activeQr = await _db.InvoiceQrs
            .Where(x => x.InvoiceId == invoiceId && x.Status == StatusActive)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeQr == null)
            return null;

        activeQr.Status = StatusDisabled;
        activeQr.DisabledAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        await _db.SaveChangesAsync(cancellationToken);
        return activeQr;
    }

    public async Task<InvoiceQr?> CloseActiveQrAsync(
        string invoiceId,
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        var effectiveQrMode = await ResolveEffectiveQrModeAsync(invoiceId, cancellationToken);
        if (string.Equals(effectiveQrMode, QrModeReuseActive, StringComparison.OrdinalIgnoreCase))
            return null;

        var activeQr = await _db.InvoiceQrs
            .Where(x => x.InvoiceId == invoiceId && x.Status == StatusActive)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeQr == null)
            return null;

        activeQr.Status = StatusDisabled;
        activeQr.Transaction = transactionId;
        activeQr.DisabledAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        await _db.SaveChangesAsync(cancellationToken);
        return activeQr;
    }

    public async Task<InvoiceQr> GenerateForInvoiceAsync(
        Invoice invoice,
        decimal? purchaseSumSom = null,
        CancellationToken cancellationToken = default)
    {
        var amountSom = purchaseSumSom ?? ResolveInvoiceAmountSom(invoice);
        if (amountSom <= 0)
            throw new InvalidOperationException("Для генерации QR сумма счёта должна быть больше 0.");

        var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

        var activeQrs = await _db.InvoiceQrs
            .Where(x => x.InvoiceId == invoice.Id && x.Status == StatusActive)
            .ToListAsync(cancellationToken);

        foreach (var existing in activeQrs)
        {
            existing.Status = StatusDisabled;
            existing.DisabledAt = now;
        }

        var qrResponse = GetStubQrResponse();

        if (qrResponse == null || qrResponse.Code != 0 || string.IsNullOrWhiteSpace(qrResponse.QrCode))
            throw new InvalidOperationException(qrResponse?.Message ?? "QR не был получен от внешнего сервиса.");

        var entity = new InvoiceQr
        {
            Id = Guid.NewGuid().ToString(),
            InvoiceId = invoice.Id,
            Transaction = null,
            Status = StatusActive,
            QrLink = qrResponse.QrLink,
            QrCodeBase64 = qrResponse.QrCode,
            CreatedAt = now
        };

        _db.InvoiceQrs.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public static InvoiceQrDto ToDto(InvoiceQr entity)
    {
        return new InvoiceQrDto
        {
            Id = entity.Id,
            InvoiceId = entity.InvoiceId,
            Transaction = entity.Transaction,
            Status = entity.Status,
            QrLink = entity.QrLink,
            QrCodeBase64 = entity.QrCodeBase64,
            CreatedAt = entity.CreatedAt,
            DisabledAt = entity.DisabledAt
        };
    }

    private static AbQrResponse GetStubQrResponse()
    {
        const string stubJson = """
        {
          "code": 0,
          "message": "Создана",
          "description": "Создана",
          "qrLink": "https://c2g.ab.kg#00020101021132740009c2g.ab.kg0105122231000113223ee0b8ca26349919e085d277f4ab9c2120212130211520499995303417540410005906нцомид3406нцомид630494f0",
          "transactId": "23ee0b8ca26349919e085d277f4ab9c2",
          "qrCode": "iVBORw0KGgoAAAANSUhEUgAAAfQAAAH0AQAAAADjreInAAADYUlEQVR4Xu2VXY7bMBCDDeT+d+0BDKThz0xsLVBA7Us94HgTSRQ/+oXIHu9/ml/HquxN+FXZm/CrsjfhV2Vvwq/K3oRflb0Jvyp7E35V9ib8quxN+FXZm/Crsjf/C38enNf7xN/52WD5PL7l3nd2hp/E80wTr+xjAPdYWyl/+Dk8PCcLwgcO1qYxRnHTKeGn8VjhIy+VfeJeoeEH82iIXeC7OqrSzRl+FF93wg+4DlZJfWIAM+0MP4qHu7ry58fO8JP4HhYI7enq0Fu9MtYTfhEeyrMV7IZwWLspqI0d3PMcfhYvxSC+cbjUSBzfwWP4SbxRNKdqQ6/tzNCnwsLP47nA5S1sIBCKP73jawg/hz9vtSkn1BdDkaE9Ux0QfgZPO2Td1fEW16zO4Qfxb3n5OXhHK1VZfXa2POHn8PTSxQhuZL7tlejM8FN4UPjjBuVglxRGvS5AdsfCT+F7aJMfe9iUzT3umsWE/+45j+V1DRd3BCpDkZ9vhPs2/Ciel+wOVxp91qBEALkpPPwQnpdXX7VIXhWJkTDrNvwgvm8QoywCJVagbsIP4yljGMDaKIA5fPAOe4oOP4Yn1iy+IEnBhvxFCT+KZzPAsSZYKLWC2G+ZvhHhp/CwcS0vTvxIcKbU8NN4McAMwGRFGc63Jfw0ngbdwG2JO+X5NWUKP4jXOKA9laq7083RIyL8BX8yj468nFBBiAFTvyt+hWLDT+IhwSdCgcLsVjDe0lHhB/GidFmWFsl74csKDz+IN1a0ZGAiVScv4afxxN/oib4772d1Kj38GN4JctJCGgFn/U85VSA/4YfxZrnqAJJOkD5WtKnwM3jcVYHkk7UuC2OePOEn8apEmXCWmV4vleX08HN46fiR4A8F7AfGbmda5yf8LF5e0grQSclO8Iu8w4T35uF84eBJMcMVOtUe7PXdePghfI16w6jaXOBX1aobFP6GP5avrsBbJQHJg+2V9lXCj+HvDpxgxBlJAO6N8oSfwqsVXFUeoNdS6UYCHOEn8mVXwFelTF0B4afyuIfCtX85nMgLSOGH8ZUCHy2yUtQ3D5UpLPwUnr25XFBEVpMvNcpu+8Lb+nD+byf8quxN+FXZm/CrsjfhV2Vvwq/K3oRflb0Jvyp7E35V9ib8quxN+FXZm8fzvwFamG3UBr0H+AAAAABJRU5ErkJggg=="
        }
        """;

        return JsonSerializer.Deserialize<AbQrResponse>(stubJson) ?? new AbQrResponse();
    }

    private static decimal ResolveInvoiceAmountSom(Invoice invoice)
    {
        var amountTyiyn =
            invoice.InvoicePayments?
                .Where(x => x.PaymentStatus == "non_paid")
                .OrderBy(x => x.DateFrom ?? DateTime.MaxValue)
                .Select(x => x.PaymentSumm)
                .FirstOrDefault(x => x.HasValue && x.Value > 0)
            ?? invoice.FixedSumm
            ?? 0m;

        return decimal.Round(amountTyiyn / 100m, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<string> ResolveEffectiveQrModeAsync(string invoiceId, CancellationToken cancellationToken)
    {
        var invoiceModeInfo = await _db.Invoices
            .AsNoTracking()
            .Where(x => x.Id == invoiceId)
            .Select(x => new
            {
                x.QrMode,
                OrganizationId = x.ClientNavigation != null ? x.ClientNavigation.Organization : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        var invoiceMode = NormalizeQrMode(invoiceModeInfo?.QrMode);
        if (invoiceMode != null)
            return invoiceMode;

        var organizationId = invoiceModeInfo?.OrganizationId;
        if (!string.IsNullOrWhiteSpace(organizationId))
        {
            var defaultQrMode = await _db.OrganizationSettings
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId)
                .Select(x => x.DefaultQrMode)
                .FirstOrDefaultAsync(cancellationToken);

            var normalizedDefaultMode = NormalizeQrMode(defaultQrMode);
            if (normalizedDefaultMode != null)
                return normalizedDefaultMode;
        }

        return QrModeRegeneratePerPeriod;
    }

    private static string? NormalizeQrMode(string? qrMode)
    {
        if (string.IsNullOrWhiteSpace(qrMode))
            return null;

        var normalized = qrMode.Trim().ToLowerInvariant();
        return normalized switch
        {
            QrModeReuseActive => QrModeReuseActive,
            QrModeRegeneratePerPeriod => QrModeRegeneratePerPeriod,
            _ => null
        };
    }
}
