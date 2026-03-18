using System.Text.Json.Serialization;

namespace WebApplication1.Models.JsonApiModels
{
    public sealed class JsonCheckRequest
    {
        [JsonPropertyName("serviceId")]
        public string ServiceId { get; set; } = string.Empty;

        [JsonPropertyName("account")]
        public string Account { get; set; } = string.Empty;
    }

    public sealed class JsonPayRequest
    {
        [JsonPropertyName("serviceId")]
        public string ServiceId { get; set; } = string.Empty;

        [JsonPropertyName("txnId")]
        public string TxnId { get; set; } = string.Empty;

        [JsonPropertyName("txnDate")]
        public string TxnDate { get; set; } = string.Empty;

        [JsonPropertyName("account")]
        public string Account { get; set; } = string.Empty;

        [JsonPropertyName("paySum")]
        public decimal PaySum { get; set; }
    }

    public sealed class JsonPaymentInfoRequest
    {
        [JsonPropertyName("txnId")]
        public string TxnId { get; set; } = string.Empty;
    }
}

