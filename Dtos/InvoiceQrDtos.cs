using System.Text.Json.Serialization;

namespace WebApplication1.Dtos;

public class GenerateInvoiceQrRequest
{
    public decimal? PurchaseSumSom { get; set; }
}

public class InvoiceQrDto
{
    public string Id { get; set; } = "";

    public string InvoiceId { get; set; } = "";

    public string? PayCode { get; set; }

    public string? Transaction { get; set; }

    public string Status { get; set; } = "";

    public string? QrLink { get; set; }

    public string? QrCodeBase64 { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DisabledAt { get; set; }
}

internal class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";
}

internal class AbQrResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("qrLink")]
    public string QrLink { get; set; } = "";

    [JsonPropertyName("qrCode")]
    public string QrCode { get; set; } = "";
}
