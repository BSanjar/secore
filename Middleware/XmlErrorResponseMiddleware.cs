using System.Text;
using System.Xml;
using System.Xml.Serialization;
using WebApplication1.Models.WebApiModels;
using WebApplication1.Services;

namespace WebApplication1.Middleware
{
    /// <summary>
    /// Middleware для перехвата ошибок валидации и замены на XML ответы
    /// </summary>
    public class XmlErrorResponseMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<XmlErrorResponseMiddleware> _logger;

        public XmlErrorResponseMiddleware(RequestDelegate next, ILogger<XmlErrorResponseMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Сохраняем оригинальный поток ответа
            var originalBodyStream = context.Response.Body;

            try
            {
                // Проверяем, что это XML запрос
                var contentType = context.Request.ContentType;
                var isXmlRequest = contentType != null && (contentType.Contains("application/xml") || contentType.Contains("text/xml"));
                
                if (isXmlRequest)
                {
                    // Создаем новый поток для перехвата ответа
                    using var responseBody = new MemoryStream();
                    context.Response.Body = responseBody;

                    await _next(context);

                    // Если статус 400 (Bad Request), заменяем на XML ответ
                    if (context.Response.StatusCode == 400)
                    {
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
                            // Очищаем текущий ответ
                            responseBody.SetLength(0);
                            context.Response.StatusCode = 200; // Возвращаем 200, так как это валидный XML ответ
                            context.Response.ContentType = "application/xml";

                            var serializer = new XmlSerializer(errorResponse.GetType());
                            var namespaces = new XmlSerializerNamespaces();
                            namespaces.Add("", "");

                            var settings = new XmlWriterSettings
                            {
                                OmitXmlDeclaration = false,
                                Encoding = Encoding.UTF8,
                                Indent = true
                            };

                            using (var writer = XmlWriter.Create(responseBody, settings))
                            {
                                serializer.Serialize(writer, errorResponse, namespaces);
                            }
                        }
                    }

                    // Копируем ответ обратно в оригинальный поток
                    responseBody.Seek(0, SeekOrigin.Begin);
                    await responseBody.CopyToAsync(originalBodyStream);
                }
                else
                {
                    await _next(context);
                }
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }
    }
}

