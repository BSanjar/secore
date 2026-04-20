using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace WebApplication1.Services;

/// <summary>
/// Запуск Chromium для PDF через PuppeteerSharp.
/// В Docker нужен системный Chromium (см. Dockerfile) и флаги --no-sandbox;
/// иначе скачанный Puppeteer-бинарник часто падает из-за отсутствия библиотек в aspnet-образе.
/// </summary>
public class PuppeteerPdfBrowserService : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly SemaphoreSlim _browserInitLock = new(1, 1);
    private IBrowser? _browser;
    private bool _disposed;

    /// <summary>
    /// Один поток на скачивание Chromium (BrowserFetcher), чтобы параллельные PDF не зависали друг на друге.
    /// </summary>
    private static readonly SemaphoreSlim ChromiumDownloadLock = new(1, 1);

    private static readonly string[] SandboxArgs =
    {
        "--no-sandbox",
        "--disable-setuid-sandbox",
        "--disable-dev-shm-usage",
        "--disable-gpu",
        "--disable-software-rasterizer",
        "--disable-background-networking",
        "--disable-extensions",
        "--disable-sync",
        "--no-first-run",
        "--no-default-browser-check",
        "--mute-audio"
    };

    /// <summary>
    /// Не ждать «тишины сети» при подстановке HTML — иначе можно зависнуть навсегда.
    /// </summary>
    private static readonly NavigationOptions PdfSetContentNavigation = new()
    {
        WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
        Timeout = 60_000
    };

    public PuppeteerPdfBrowserService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Рендер HTML в PDF (A4, поля 8mm, фон печати).
    /// </summary>
    public async Task<byte[]> RenderPdfFromHtmlAsync(string html, CancellationToken cancellationToken = default)
    {
        var browser = await LaunchBrowserAsync(cancellationToken).ConfigureAwait(false);
        await using var page = await browser.NewPageAsync().ConfigureAwait(false);
        await page.SetContentAsync(html, PdfSetContentNavigation).ConfigureAwait(false);
        return await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            DisplayHeaderFooter = false,
            MarginOptions = new MarginOptions { Top = "8mm", Right = "8mm", Bottom = "8mm", Left = "8mm" }
        }).ConfigureAwait(false);
    }

    public async Task<IBrowser> LaunchBrowserAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_browser is { IsClosed: false })
            return _browser;

        await _browserInitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_browser is { IsClosed: false })
                return _browser;

            if (_browser is not null)
            {
                await _browser.DisposeAsync().ConfigureAwait(false);
                _browser = null;
            }

            var launchOptions = BuildLaunchOptions();
            if (string.IsNullOrEmpty(launchOptions.ExecutablePath))
            {
                await ChromiumDownloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await new BrowserFetcher().DownloadAsync().ConfigureAwait(false);
                }
                finally
                {
                    ChromiumDownloadLock.Release();
                }
            }

            _browser = await Puppeteer.LaunchAsync(launchOptions).ConfigureAwait(false);
            return _browser;
        }
        finally
        {
            _browserInitLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_browser is not null)
        {
            await _browser.DisposeAsync().ConfigureAwait(false);
            _browser = null;
        }

        _browserInitLock.Dispose();
    }

    private LaunchOptions BuildLaunchOptions()
    {
        var path = ResolveChromiumPath();
        var options = new LaunchOptions
        {
            Headless = true,
            Args = SandboxArgs,
            // Критично для ASP.NET: иначе ответы CDP могут маршрутизироваться через SyncContext запроса и вызывать вечное ожидание.
            EnqueueAsyncMessages = true,
            Timeout = 120_000,
            ProtocolTimeout = 120_000
        };

        if (!string.IsNullOrEmpty(path))
        {
            options.ExecutablePath = path;
            // Иначе PuppeteerSharp считает браузер «Chrome» и несовпадение с системным Chromium ломает запуск/CDP.
            options.Browser = SupportedBrowser.Chromium;
            // New headless (HeadlessMode.True) часто не подходит для chromium из Debian/Ubuntu в Docker.
            options.HeadlessMode = HeadlessMode.Shell;
        }

        return options;
    }

    private string? ResolveChromiumPath()
    {
        var configured = _configuration["Pdf:ChromiumExecutablePath"];
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        var envPath = Environment.GetEnvironmentVariable("CHROMIUM_EXECUTABLE_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            return envPath;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return null;

        foreach (var candidate in new[]
                 {
                     "/usr/bin/chromium",
                     "/usr/bin/chromium-browser",
                     "/usr/lib/chromium/chromium",
                     "/usr/lib/chromium-browser/chromium-browser",
                     "/usr/bin/google-chrome-stable",
                     "/usr/bin/google-chrome"
                 })
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
