using System.Xml.Serialization;

namespace WebApplication1.Models.WebApiModels
{
    [XmlRoot("check_account")]
    public class CheckRequest
    {
        [XmlElement("login")]
        public string Login { get; set; } = string.Empty;

        [XmlElement("password")]
        public string Password { get; set; } = string.Empty;

        [XmlElement("operator")]
        public string Operator { get; set; } = string.Empty;

        [XmlElement("account")]
        public string Account { get; set; } = string.Empty;
    }
}
