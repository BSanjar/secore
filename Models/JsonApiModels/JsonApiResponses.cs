using System.Text.Json.Serialization;

namespace WebApplication1.Models.JsonApiModels
{
    public class JsonBaseResponse
    {
        [JsonPropertyOrder(-2)]
        [JsonPropertyName("result")]
        public int Result {     get; set; }

        [JsonPropertyOrder(-1)]
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    public sealed class JsonClientInfo
    {
        [JsonPropertyName("inn")]
        public string Inn { get; set; } = string.Empty;

        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("phone")]
        public string Phone { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public sealed class JsonInvoiceForPaymentItem
    {
        [JsonPropertyName("invoiceName")]
        public string InvoiceName { get; set; } = string.Empty;

        [JsonPropertyName("period")]
        public string Period { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }
    }

    public sealed class JsonCheckResponse : JsonBaseResponse
    {
        [JsonPropertyOrder(0)]
        [JsonPropertyName("account")]
        public long Account { get; set; }

        [JsonPropertyOrder(1)]
        [JsonPropertyName("balanceSum")]
        public decimal BalanceSum { get; set; }

        [JsonPropertyOrder(2)]
        [JsonPropertyName("recomendedPaySum")]
        public decimal RecomendedPaySum { get; set; }

        [JsonPropertyOrder(3)]
        [JsonPropertyName("organization")]
        public string Organization { get; set; } = string.Empty;

        [JsonPropertyOrder(4)]
        [JsonPropertyName("subscriber")]
        public string Subscriber { get; set; } = string.Empty;

        [JsonPropertyOrder(5)]
        [JsonPropertyName("client")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonClientInfo? Client { get; set; }

        [JsonPropertyOrder(6)]
        [JsonPropertyName("invoicesForPayment")]
        public List<JsonInvoiceForPaymentItem> InvoicesForPayment { get; set; } = new();
    }

    public sealed class JsonPayResponse : JsonBaseResponse
    {
        [JsonPropertyName("account")]
        public string Account { get; set; } = string.Empty;

        [JsonPropertyName("secoreTxnId")]
        public string SecoreTxnId { get; set; } = string.Empty;

        [JsonPropertyName("txnId")]
        public string TxnId { get; set; } = string.Empty;

        [JsonPropertyName("txnDate")]
        public string TxnDate { get; set; } = string.Empty;

        [JsonPropertyName("transactionDateTime")]
        public string TransactionDateTime { get; set; } = string.Empty;

        [JsonPropertyName("balanceSum")]
        public decimal BalanceSum { get; set; }

        [JsonPropertyName("paidSum")]
        public decimal PaidSum { get; set; }

        [JsonPropertyName("balanceAdded")]
        public decimal BalanceAdded { get; set; }

        [JsonPropertyName("paidInvoices")]
        public string PaidInvoices { get; set; } = string.Empty;
    }

    public sealed class JsonPaymentInfoResponse : JsonBaseResponse
    {
        [JsonPropertyName("secoreTxnId")]
        public string SecoreTxnId { get; set; } = string.Empty;

        [JsonPropertyName("txnId")]
        public string TxnId { get; set; } = string.Empty;

        [JsonPropertyName("paymentStatus")]
        public string PaymentStatus { get; set; } = string.Empty;

        [JsonPropertyName("transactionDateTime")]
        public string TransactionDateTime { get; set; } = string.Empty;
    }
}

