using System.Xml.Serialization;

namespace WebApplication1.Models.WebApiModels
{
    [XmlRoot("response", Namespace = "")]
    public class PaymentInfoResponse
    {
        [XmlElement("result")]
        public int Result { get; set; }

        [XmlElement("description")]
        public string Description { get; set; } = string.Empty;

        [XmlElement("txn_id")]
        public string? TxnId { get; set; }

        [XmlElement("payment_status")]
        public string? PaymentStatus { get; set; }
    }
}
