using System.Xml.Serialization;

namespace WebApplication1.Models.WebApiModels
{
    [XmlRoot("response", Namespace = "")]
    public class CheckResponse
    {
        [XmlElement("account")]
        public string Account { get; set; } = string.Empty;

        [XmlElement("wallet_account")]
        public string WalletAccount { get; set; } = string.Empty;

        [XmlElement("service")]
        public string Service { get; set; } = string.Empty;

        [XmlElement("result")]
        public int Result { get; set; }

        [XmlElement("description")]
        public string Description { get; set; } = string.Empty;

        [XmlElement("additional")]
        public AdditionalInfo? Additional { get; set; }

        [XmlElement("payer_name")]
        public string PayerName { get; set; } = string.Empty;
    }

    public class AdditionalInfo
    {
        [XmlElement("item")]
        public List<AdditionalItem> Items { get; set; } = new List<AdditionalItem>();
    }

    public class AdditionalItem
    {
        [XmlAttribute("name")]
        public string Name { get; set; } = string.Empty;

        [XmlText]
        public string Value { get; set; } = string.Empty;
    }
}
