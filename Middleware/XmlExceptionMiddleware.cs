using System.Text;
using System.Xml;
using System.Xml.Serialization;
using WebApplication1.Models.WebApiModels;
using WebApplication1.Services;

namespace WebApplication1.Middleware
{
    /// <summary>
    /// Middleware для обработки исключений при десериализации XML
    /// </summary>
    public class XmlExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<XmlExceptionMiddleware> _logger;

        public XmlExceptionMiddleware(RequestDelegate next, ILogger<XmlExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                // Проверяем, что это XML запрос и ошибка связана с десериализацией
                var contentType = context.Request.ContentType;
                if (contentType != null && (contentType.Contains("application/xml") || contentType.Contains("text/xml")))
                {
                    // Проверяем, является ли это ошибкой десериализации
                    if (ex is InvalidOperationException || ex is XmlException || ex.Message.Contains("deserializ"))
                    {
                        _logger.LogWarning(ex, "XML deserialization error");

                        var path = context.Request.Path.Value?.ToLower() ?? "";
                        object? errorResponse = null;

                        if (path.Contains("/check"))
                        {
                            errorResponse = WebApiResponseService.CreateCheckErrorResponse(ErrorCode.UnknownRequest);
                        }
                        else if (path.Contains("/pay"))
                        {
                            if (path.Contains("/payinfo"))
                            {
                                errorResponse = WebApiResponseService.CreatePayInfoErrorResponse(ErrorCode.UnknownRequest);
                            }
                            else
                            {
                                errorResponse = WebApiResponseService.CreatePayErrorResponse(ErrorCode.UnknownRequest);
                            }
                        }

                        if (errorResponse != null)
                        {
                            context.Response.StatusCode = 200; // Возвращаем 200, так как это валидный XML ответ
                            context.Response.ContentType = "application/xml";

                            var serializer = new XmlSerializer(errorResponse.GetType());
                            var namespaces = new XmlSerializerNamespaces();
                            namespaces.Add("", "");

                            var settings = new XmlWriterSettings
                            {
                                OmitXmlDeclaration = false,
                                Encoding = Encoding.UTF8,
                                Indent = true,
                                Async = true
                            };

                            using (var ms = new MemoryStream())
                            {
                                using (var writer = XmlWriter.Create(ms, settings))
                                {
                                    serializer.Serialize(writer, errorResponse, namespaces);
                                }
                                ms.Position = 0;
                                await ms.CopyToAsync(context.Response.Body);
                            }

                            return;
                        }
                    }
                }

                // Если это не XML ошибка, пробрасываем дальше
                throw;
            }
        }
    }
}

