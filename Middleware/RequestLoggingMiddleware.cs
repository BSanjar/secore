using System.Diagnostics;

namespace WebApplication1.Middleware;

/// <summary>
/// Logs significant HTTP traffic with date/time, user, org, status and duration.
/// Static assets are skipped to keep daily files readable.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private static readonly HashSet<string> SkipPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/css", "/js", "/lib", "/web", "/img", "/favicon", "/swagger", "/_framework", "/swagger-ui"
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (ShouldSkip(path))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            var status = context.Response?.StatusCode ?? 0;
            var method = context.Request.Method;
            string userId = "-";
            string orgId = "-";
            try
            {
                userId = context.Session?.GetString("UserId") ?? "-";
                orgId = context.Session?.GetString("OrganizationId") ?? "-";
            }
            catch
            {
                // Session may be unavailable for some edge requests
            }
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "-";
            var query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;

            var isMutating = HttpMethods.IsPost(method)
                             || HttpMethods.IsPut(method)
                             || HttpMethods.IsDelete(method)
                             || HttpMethods.IsPatch(method);
            var isApi = path.StartsWith("/avnWebApi", StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith("/WebApi", StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith("/api", StringComparison.OrdinalIgnoreCase);

            if (status >= 500)
            {
                _logger.LogError(
                    "HTTP {Method} {Path}{Query} -> {StatusCode} in {ElapsedMs}ms | User={UserId} Org={OrgId} IP={Ip}",
                    method, path, query, status, sw.ElapsedMilliseconds, userId, orgId, ip);
            }
            else if (status >= 400 || isMutating || isApi || sw.ElapsedMilliseconds >= 3000)
            {
                _logger.LogWarning(
                    "HTTP {Method} {Path}{Query} -> {StatusCode} in {ElapsedMs}ms | User={UserId} Org={OrgId} IP={Ip}",
                    method, path, query, status, sw.ElapsedMilliseconds, userId, orgId, ip);
            }
            else
            {
                _logger.LogInformation(
                    "HTTP {Method} {Path}{Query} -> {StatusCode} in {ElapsedMs}ms | User={UserId} Org={OrgId} IP={Ip}",
                    method, path, query, status, sw.ElapsedMilliseconds, userId, orgId, ip);
            }
        }
    }

    private static bool ShouldSkip(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
            return false;

        foreach (var prefix in SkipPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var ext = Path.GetExtension(path);
        return ext is ".css" or ".js" or ".map" or ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".ico" or ".woff" or ".woff2" or ".ttf" or ".svg";
    }
}
