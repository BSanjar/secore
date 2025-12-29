using System.Xml.Serialization;

namespace WebApplication1.Models.WebApiModels
{
    [XmlRoot("response", Namespace = "")]
    public class PayResponse
    {
        [XmlElement("account")]
        public string? Account { get; set; }

        [XmlElement("wallet_account")]
        public string? WalletAccount { get; set; }

        [XmlElement("service")]
        public string? Service { get; set; }

        [XmlElement("result")]
        public int Result { get; set; }

        [XmlElement("description")]
        public string Description { get; set; } = string.Empty;

        [XmlElement("additional")]
        public AdditionalInfo? Additional { get; set; }

        [XmlElement("payer_name")]
        public string? PayerName { get; set; }

        [XmlElement("avn_txn_id")]
        public string? AvnTxnId { get; set; }

        [XmlElement("txn_id")]
        public string? TxnId { get; set; }

        [XmlElement("txn_date")]
        public string? TxnDate { get; set; }
    }
}
