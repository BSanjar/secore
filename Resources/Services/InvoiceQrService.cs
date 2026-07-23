using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebApplication1.Dtos;
using WebApplication1.Models.BaseModels;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Services;

/// <summary>
/// Единая точка работы с внешним QR-сервисом и таблицей invoice_qr.
/// Один QR на лицевой счёт (pay_code); при оплате или новом счёте с тем же ЛС — обновление записи, не новая.
/// </summary>
public class InvoiceQrService
{
    public const string StatusActive = "active";
    public const string StatusDisabled = "disabled";

    private readonly AppDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly AbQrOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvoiceQrService> _logger;

    public InvoiceQrService(
        AppDbContext db,
        HttpClient httpClient,
        IOptions<AbQrOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<InvoiceQrService> logger)
    {
        _db = db;
        _httpClient = httpClient;
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<InvoiceQr?> GetActiveQrAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        var payCode = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => i.PayCode)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(payCode))
            return null;

        return await GetQrByPayCodeAsync(payCode, cancellationToken);
    }

    public async Task<InvoiceQr?> GetQrByPayCodeAsync(string payCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(payCode))
            return null;

        var normalized = payCode.Trim();
        return await _db.InvoiceQrs
            .AsNoTracking()
            .Where(x => x.PayCode == normalized && x.Status == StatusActive)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<InvoiceQr?> DisableActiveQrAsync(string invoiceId, CancellationToken cancellationToken = default)
    {
        var payCode = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => i.PayCode)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(payCode))
            return null;

        var qr = await _db.InvoiceQrs
            .Where(x => x.PayCode == payCode.Trim() && x.Status == StatusActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (qr == null)
            return null;

        qr.Status = StatusDisabled;
        qr.DisabledAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        await _db.SaveChangesAsync(cancellationToken);
        return qr;
    }

    /// <summary>Синхронное обновление QR (ручная генерация из UI).</summary>
    public Task<InvoiceQr> GenerateForInvoiceAsync(
        Invoice invoice,
        decimal? purchaseSumSom = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(invoice.PayCode))
            throw new InvalidOperationException("У счёта отсутствует лицевой счёт (PayCode) для QR.");

        return RefreshQrForPayCodeAsync(
            invoice.PayCode.Trim(),
            invoice.Id,
            purchaseSumSom,
            cancellationToken);
    }

    /// <summary>Не блокирует HTTP-запрос: обновление QR в фоне после commit транзакции.</summary>
    public void EnqueueRefreshForPayCode(string payCode, string anchorInvoiceId)
    {
        if (string.IsNullOrWhiteSpace(payCode) || string.IsNullOrWhiteSpace(anchorInvoiceId))
            return;

        var code = payCode.Trim();
        var anchor = anchorInvoiceId.Trim();

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<InvoiceQrService>();
                await svc.RefreshQrForPayCodeAsync(code, anchor, purchaseSumSom: null, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background QR refresh failed. PayCode={PayCode} InvoiceId={InvoiceId}", code, anchor);
            }
        });
    }

    public void EnqueueRefreshForInvoice(Invoice invoice)
    {
        if (invoice == null || string.IsNullOrWhiteSpace(invoice.PayCode))
            return;
        EnqueueRefreshForPayCode(invoice.PayCode, invoice.Id);
    }

    /// <summary>
    /// Запрос QR у внешнего API (сейчас эмуляция) и upsert одной строки invoice_qr на pay_code.
    /// </summary>
    public async Task<InvoiceQr> RefreshQrForPayCodeAsync(
        string payCode,
        string anchorInvoiceId,
        decimal? purchaseSumSom = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(payCode))
            throw new InvalidOperationException("PayCode не указан.");
        if (string.IsNullOrWhiteSpace(anchorInvoiceId))
            throw new InvalidOperationException("Не указан счёт-якорь для QR.");

        var normalizedPayCode = payCode.Trim();
        var amountSom = purchaseSumSom ?? await ResolveAmountSomForPayCodeAsync(normalizedPayCode, cancellationToken);
        if (amountSom <= 0)
            throw new InvalidOperationException("Для генерации QR сумма должна быть больше 0.");

        var apiResponse = await RequestQrFromExternalServiceAsync(normalizedPayCode, amountSom, cancellationToken);
        if (apiResponse == null || apiResponse.Code != 0 || string.IsNullOrWhiteSpace(apiResponse.QrCode))
            throw new InvalidOperationException(apiResponse?.Message ?? "QR не был получен от внешнего сервиса.");

        var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

        var existing = await _db.InvoiceQrs
            .FirstOrDefaultAsync(x => x.PayCode == normalizedPayCode, cancellationToken);

        if (existing != null)
        {
            existing.InvoiceId = anchorInvoiceId;
            existing.Status = StatusActive;
            existing.QrLink = apiResponse.QrLink;
            existing.QrCodeBase64 = apiResponse.QrCode;
            existing.DisabledAt = null;
            existing.Transaction = null;
            existing.CreatedAt = now;
        }
        else
        {
            existing = new InvoiceQr
            {
                Id = Guid.NewGuid().ToString(),
                PayCode = normalizedPayCode,
                InvoiceId = anchorInvoiceId,
                Transaction = null,
                Status = StatusActive,
                QrLink = apiResponse.QrLink,
                QrCodeBase64 = apiResponse.QrCode,
                CreatedAt = now
            };
            _db.InvoiceQrs.Add(existing);
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "QR refreshed. PayCode={PayCode} InvoiceId={InvoiceId} AmountSom={Amount}",
            normalizedPayCode,
            anchorInvoiceId,
            amountSom);

        return existing;
    }

    public static InvoiceQrDto ToDto(InvoiceQr entity)
    {
        return new InvoiceQrDto
        {
            Id = entity.Id,
            InvoiceId = entity.InvoiceId,
            PayCode = entity.PayCode,
            Transaction = entity.Transaction,
            Status = entity.Status,
            QrLink = entity.QrLink,
            QrCodeBase64 = entity.QrCodeBase64,
            CreatedAt = entity.CreatedAt,
            DisabledAt = entity.DisabledAt
        };
    }

    /// <summary>Вызов стороннего QR API (пока заглушка; далее — OAuth + POST на QrUrl из AbQrOptions).</summary>
    private async Task<AbQrResponse?> RequestQrFromExternalServiceAsync(
        string payCode,
        decimal amountSom,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.QrUrl))
        {
            try     
            {
                // TODO: token + реальный запрос к AB QR API с payCode и amountSom.
                _ = payCode;
                _ = amountSom;
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "External QR API failed, using stub. PayCode={PayCode}", payCode);
            }
        }

        return GetStubQrResponse();
    }

    private async Task<decimal> ResolveAmountSomForPayCodeAsync(string payCode, CancellationToken cancellationToken)
    {
        var invoiceIds = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.PayCode == payCode && i.InvoiceStatus == "actual")
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);

        if (invoiceIds.Count == 0)
        {
            var anyId = await _db.Invoices
                .AsNoTracking()
                .Where(i => i.PayCode == payCode)
                .OrderByDescending(i => i.DateCreated)
                .Select(i => i.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (anyId != null)
                invoiceIds.Add(anyId);
        }

        if (invoiceIds.Count == 0)
            return 0m;

        var payments = await _db.InvoicePayments
            .AsNoTracking()
            .Where(p => p.Invoice != null && invoiceIds.Contains(p.Invoice) && p.PaymentStatus == "non_paid")
            .OrderBy(p => p.DateFrom)
            .Select(p => p.PaymentSumm)
            .ToListAsync(cancellationToken);

        var tyiyn = payments.FirstOrDefault(x => x.HasValue && x.Value > 0)
            ?? await _db.Invoices
                .AsNoTracking()
                .Where(i => invoiceIds.Contains(i.Id))
                .Select(i => i.FixedSumm)
                .FirstOrDefaultAsync(cancellationToken);

        return decimal.Round((tyiyn ?? 0m) / 100m, 2, MidpointRounding.AwayFromZero);
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
}
