using System.Xml.Serialization;

namespace WebApplication1.Models.WebApiModels
{
    [XmlRoot("payment_info")]
    public class PaymentInfoRequest
    {
        [XmlElement("login")]
        public string Login { get; set; } = string.Empty;

        [XmlElement("password")]
        public string Password { get; set; } = string.Empty;

        [XmlElement("operator")]
        public string Operator { get; set; } = string.Empty;

        [XmlElement("txn_id")]
        public string TxnId { get; set; } = string.Empty;
    }
}
