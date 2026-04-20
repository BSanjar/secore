namespace WebApplication1.Models.BaseModels;

public class AbQrOptions
{
    public const string SectionName = "AbQr";

    public string TokenUrl { get; set; } = "";

    public string QrUrl { get; set; } = "";

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    public string ClientId { get; set; } = "";

    public int ServiceId { get; set; }

    public string ProviderName { get; set; } = "";

    public string MccCode { get; set; } = "";

    public decimal FeeSum { get; set; }
}
