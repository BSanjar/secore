using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace WebApplication1.Formatters
{
    public class CustomXmlSerializerOutputFormatter : XmlSerializerOutputFormatter
    {
        public CustomXmlSerializerOutputFormatter()
        {
            SupportedMediaTypes.Clear();
            SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/xml"));
            SupportedMediaTypes.Add(new MediaTypeHeaderValue("text/xml"));
            
            // Настраиваем WriterSettings для включения XML декларации
            WriterSettings.OmitXmlDeclaration = false;
            WriterSettings.Encoding = Encoding.UTF8;
        }

        protected override void Serialize(XmlSerializer xmlSerializer, XmlWriter xmlWriter, object? value)
        {
            var namespaces = new XmlSerializerNamespaces();
            namespaces.Add("", ""); // Добавляем пустое пространство имен, чтобы убрать xmlns:xsi и xmlns:xsd
            xmlSerializer.Serialize(xmlWriter, value, namespaces);
        }
    }
}

