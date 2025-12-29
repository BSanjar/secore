using System.Xml.Serialization;

namespace WebApplication1.Models.WebApiModels
{
    [XmlRoot("payment")]
    public class PayRequest
    {
        [XmlElement("login")]
        public string Login { get; set; } = string.Empty;

        [XmlElement("password")]
        public string Password { get; set; } = string.Empty;

        [XmlElement("operator")]
        public string Operator { get; set; } = string.Empty;

        [XmlElement("txn_id")]
        public string TxnId { get; set; } = string.Empty;

        [XmlElement("txn_date")]
        public string TxnDate { get; set; } = string.Empty;

        [XmlElement("account")]
        public string Account { get; set; } = string.Empty;

        [XmlElement("payer_name")]
        public string PayerName { get; set; } = string.Empty;

        [XmlElement("sum")]
        public string Sum { get; set; } = string.Empty;

        [XmlElement("payment_method")]
        public string PaymentMethod { get; set; } = string.Empty;
    }
}
