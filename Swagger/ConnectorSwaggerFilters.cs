using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Text;
using WebApplication1.Models.JsonApiModels;
using WebApplication1.Models.WebApiModels;
using WebApplication1.Services;

namespace WebApplication1.Swagger
{
    public sealed class ConnectorDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Connector endpoints");
            sb.AppendLine();
            sb.AppendLine("- **JSON**: `POST /WebApi/check`, `POST /WebApi/pay`, `POST /WebApi/payInfo`");
            sb.AppendLine();
            sb.AppendLine("## Auth (JSON)");
            sb.AppendLine();
            sb.AppendLine("Use **Basic Auth** header:");
            sb.AppendLine();
            sb.AppendLine("`Authorization: Basic base64(login:password)`");
            sb.AppendLine();
            sb.AppendLine("## Error codes");
            sb.AppendLine();
            sb.AppendLine("| Code | Name | Description |");
            sb.AppendLine("|------|------|-------------|");

            foreach (var value in Enum.GetValues<ErrorCode>().OrderBy(v => (int)v))
            {
                var desc = WebApiResponseService.GetErrorDescription(value)
                    .Replace("\r", string.Empty)
                    .Replace("\n", " ");
                sb.AppendLine($"| {(int)value} | `{value}` | {desc} |");
            }

            swaggerDoc.Info.Description = sb.ToString();
        }
    }

    public sealed class ConnectorOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var route = context.ApiDescription.RelativePath ?? string.Empty;

            // Tag by connector type
            if (route.StartsWith("WebApi/", StringComparison.OrdinalIgnoreCase))
                operation.Tags = new List<OpenApiTag> { new() { Name = "JSON connector" } };

            // Add basic auth requirement for JSON connector
            if (route.StartsWith("WebApi/", StringComparison.OrdinalIgnoreCase))
            {
                operation.Security ??= new List<OpenApiSecurityRequirement>();
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "basicAuth"
                        }
                    }] = Array.Empty<string>()
                });
            }

            // Make responses more explicit (even if models already have them)
            operation.Responses.TryAdd("200", new OpenApiResponse { Description = "Business-level response (see result/description)" });
            operation.Responses.TryAdd("400", new OpenApiResponse { Description = "Invalid request body / model binding error" });
            operation.Responses.TryAdd("500", new OpenApiResponse { Description = "Server error" });

            // Examples (JSON only) – small but helpful
            if (route.Equals("WebApi/check", StringComparison.OrdinalIgnoreCase))
            {
                operation.Summary = "Check account (JSON)";
                operation.Description = "Returns balance, recommended payment sum and invoices list.";
            }
            else if (route.Equals("WebApi/pay", StringComparison.OrdinalIgnoreCase))
            {
                operation.Summary = "Pay (JSON)";
                operation.Description = "Accepts payment, applies it to debts/schedule and updates balance.";
            }
            else if (route.Equals("WebApi/payInfo", StringComparison.OrdinalIgnoreCase))
            {
                operation.Summary = "Payment status (JSON)";
                operation.Description = "Returns paymentStatus: `1` success, `0` not success, `3` not found. `serviceId` is not required for this request.";
            }
        }
    }

    public sealed class ConnectorSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            // Temporary contract change: do not expose `client` in check response schema
            if (context.Type == typeof(JsonCheckResponse))
            {
                schema.Properties?.Remove("client");
            }
        }
    }

    public sealed class SortSchemasDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            if (swaggerDoc.Components?.Schemas == null)
                return;

            swaggerDoc.Components.Schemas = swaggerDoc.Components.Schemas
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }
    }
}

