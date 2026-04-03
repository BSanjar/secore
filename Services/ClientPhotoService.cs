using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace WebApplication1.Services;

/// <summary>
/// Сохранение и сжатие фото клиента (организации) на диске под wwwroot.
/// </summary>
public class ClientPhotoService
{
    public const int MaxUploadBytes = 2 * 1024 * 1024; // 2 МБ до обработки
    public const int MaxEdgePx = 400;
    public const int JpegQuality = 82;

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ClientPhotoService> _logger;

    public ClientPhotoService(IWebHostEnvironment env, ILogger<ClientPhotoService> logger)
    {
        _env = env;
        _logger = logger;
    }

    private static string RelativeUrl(string organizationId, string clientId) =>
        $"/uploads/clients/{SanitizeSegment(organizationId)}/{SanitizeSegment(clientId)}.jpg";

    private static string SanitizeSegment(string id)
    {
        if (string.IsNullOrEmpty(id)) return "_";
        var chars = id.ToCharArray();
        var invalid = Path.GetInvalidFileNameChars();
        for (var i = 0; i < chars.Length; i++)
        {
            if (invalid.Contains(chars[i]) || chars[i] == '/' || chars[i] == '\\')
                chars[i] = '_';
        }
        return new string(chars);
    }

    private string PhysicalPath(string organizationId, string clientId) =>
        Path.Combine(_env.WebRootPath, "uploads", "clients", SanitizeSegment(organizationId), SanitizeSegment(clientId) + ".jpg");

    /// <summary>
    /// Создаёт сжатый JPEG, возвращает относительный URL для ClientLogo.
    /// </summary>
    public async Task<string> SaveAndCompressAsync(
        IFormFile file,
        string organizationId,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            throw new InvalidOperationException("Файл не выбран.");

        if (file.Length > MaxUploadBytes)
            throw new InvalidOperationException($"Файл не должен превышать {MaxUploadBytes / 1024 / 1024} МБ.");

        if (!IsAllowedImage(file.ContentType, file.FileName))
            throw new InvalidOperationException("Допустимы только изображения: JPEG, PNG, GIF, WebP.");

        var dir = Path.GetDirectoryName(PhysicalPath(organizationId, clientId));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var destPath = PhysicalPath(organizationId, clientId);

        await using var readStream = file.OpenReadStream();
        await using var ms = new MemoryStream();
        await readStream.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;

        using var image = await Image.LoadAsync(ms, cancellationToken);

        var w = image.Width;
        var h = image.Height;
        if (w > MaxEdgePx || h > MaxEdgePx)
        {
            var ratio = Math.Min((double)MaxEdgePx / w, (double)MaxEdgePx / h);
            var nw = Math.Max(1, (int)Math.Round(w * ratio));
            var nh = Math.Max(1, (int)Math.Round(h * ratio));
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(nw, nh),
                Mode = ResizeMode.Max
            }));
        }

        var encoder = new JpegEncoder { Quality = JpegQuality };
        await image.SaveAsJpegAsync(destPath, encoder, cancellationToken);

        return RelativeUrl(organizationId, clientId);
    }

    private static bool IsAllowedImage(string? contentType, string? fileName)
    {
        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (contentType != null)
        {
            var ct = contentType.ToLowerInvariant();
            if (ct is "image/jpeg" or "image/pjpeg" or "image/png" or "image/gif" or "image/webp")
                return true;
        }
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp";
    }

    public void DeleteFileIfExists(string? clientLogoRelativePath)
    {
        if (string.IsNullOrEmpty(clientLogoRelativePath) || !clientLogoRelativePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return;
        try
        {
            var relative = clientLogoRelativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var full = Path.Combine(_env.WebRootPath, relative);
            if (File.Exists(full))
                File.Delete(full);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось удалить файл фото: {Path}", clientLogoRelativePath);
        }
    }
}
