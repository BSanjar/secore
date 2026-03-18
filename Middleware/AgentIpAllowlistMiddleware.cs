using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Security;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;
using WebApplication1.Models.WebApiModels;
using WebApplication1.Services;

namespace WebApplication1.Middleware;

public sealed class AgentIpAllowlistMiddleware
{
    private readonly RequestDelegate _next;

    public AgentIpAllowlistMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, AppDbContext db)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/WebApi/", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/avnWebApi/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var clientIp = GetClientIp(context);
        var login = await TryExtractAgentLoginAsync(context, path);

        // Если логин не извлекли — пусть дальше отработает стандартная аутентификация/валидация.
        if (string.IsNullOrWhiteSpace(login))
        {
            await _next(context);
            return;
        }

        var agent = await db.Agents.FirstOrDefaultAsync(a => a.ApiLogin == login);
        if (agent == null)
        {
            await _next(context);
            return;
        }

        // Пусто/NULL = разрешены все IP (обратная совместимость)
        if (string.IsNullOrWhiteSpace(agent.AllowlistIp))
        {
            await _next(context);
            return;
        }

        var allowed = ParseAllowlist(agent.AllowlistIp);
        if (allowed.Count == 0)
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrWhiteSpace(clientIp) || !allowed.Contains(clientIp))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;

            if (path.StartsWith("/WebApi/", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                var body = $"{{\"result\":{(int)ErrorCode.IpNotAllowed},\"description\":\"{EscapeJson(WebApiResponseService.GetErrorDescription(ErrorCode.IpNotAllowed))}\"}}";
                await context.Response.WriteAsync(body);
                return;
            }

            context.Response.ContentType = "application/xml; charset=utf-8";
            var xml = $"<response><result>{(int)ErrorCode.IpNotAllowed}</result><description>{SecurityElement.Escape(WebApiResponseService.GetErrorDescription(ErrorCode.IpNotAllowed))}</description></response>";
            await context.Response.WriteAsync(xml);
            return;
        }

        await _next(context);
    }

    private static HashSet<string> ParseAllowlist(string raw)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in raw.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var ip = part.Trim();
            if (ip.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase))
                ip = ip.Substring("::ffff:".Length);
            if (!string.IsNullOrWhiteSpace(ip))
                set.Add(ip);
        }
        return set;
    }

    private static string? GetClientIp(HttpContext context)
    {
        var xff = context.Request.Headers["X-Forwarded-For"].ToString();
        var raw = !string.IsNullOrWhiteSpace(xff)
            ? xff.Split(',')[0].Trim()
            : context.Connection.RemoteIpAddress?.ToString();

        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (raw.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase))
            raw = raw.Substring("::ffff:".Length);

        return raw;
    }

    private static async Task<string?> TryExtractAgentLoginAsync(HttpContext context, string path)
    {
        if (path.StartsWith("/WebApi/", StringComparison.OrdinalIgnoreCase))
        {
            if (!context.Request.Headers.TryGetValue("Authorization", out var authHeaderValues))
                return null;

            if (!AuthenticationHeaderValue.TryParse(authHeaderValues.FirstOrDefault(), out var headerValue))
                return null;

            if (!string.Equals(headerValue.Scheme, "Basic", StringComparison.OrdinalIgnoreCase))
                return null;

            if (string.IsNullOrWhiteSpace(headerValue.Parameter))
                return null;

            string decoded;
            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(headerValue.Parameter));
            }
            catch
            {
                return null;
            }

            var idx = decoded.IndexOf(':');
            if (idx <= 0)
                return null;

            var login = decoded.Substring(0, idx);
            return string.IsNullOrWhiteSpace(login) ? null : login;
        }

        // avnWebApi (XML): login лежит в body. Нужно буферизировать, чтобы контроллер потом смог прочитать body.
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(body))
            return null;

        var m = Regex.Match(body, "<login>(.*?)</login>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success)
            return null;

        var loginXml = m.Groups[1].Value?.Trim();
        return string.IsNullOrWhiteSpace(loginXml) ? null : loginXml;
    }

    private static string EscapeJson(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

