using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Net.Http.Headers;

namespace WebApplication1.Formatters
{
    /// <summary>
    /// Кастомный XML Input Formatter, который обрабатывает ошибки десериализации
    /// </summary>
    public class CustomXmlSerializerInputFormatter : XmlSerializerInputFormatter
    {
        public CustomXmlSerializerInputFormatter(MvcOptions options) : base(options)
        {
            SupportedMediaTypes.Clear();
            SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/xml"));
            SupportedMediaTypes.Add(new MediaTypeHeaderValue("text/xml"));
        }

        public override async Task<InputFormatterResult> ReadRequestBodyAsync(
            InputFormatterContext context,
            Encoding encoding)
        {
            try
            {
                return await base.ReadRequestBodyAsync(context, encoding);
            }
            catch (Exception ex)
            {
                // Если произошла ошибка десериализации, возвращаем null
                // Фильтр XmlValidationFilter обработает это и вернет правильный XML ответ
                return await InputFormatterResult.NoValueAsync();
            }
        }
    }
}

