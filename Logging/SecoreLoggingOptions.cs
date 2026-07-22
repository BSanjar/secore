namespace WebApplication1.Logging;

/// <summary>
/// Persistent file logging outside the container.
/// Server layout:
///   /secore/mainSystem   — this web app (mainSystem-YYYYMMDD.log)
///   /secore/notificator  — notificator service
///   /secore/sheduler     — payment scheduler service
/// </summary>
public sealed class SecoreLoggingOptions
{
    public const string SectionName = "SecoreLogging";

    /// <summary>Host folder mounted into the container. Default: /secore/mainSystem</summary>
    public string RootPath { get; set; } = "/secore/mainSystem";

    /// <summary>File name prefix. Daily files: {FilePrefix}-20260722.log</summary>
    public string FilePrefix { get; set; } = "mainSystem";

    /// <summary>How many daily files to keep (null = unlimited).</summary>
    public int? RetainedFileCountLimit { get; set; } = 120;

    /// <summary>Minimum level written to file.</summary>
    public string MinimumLevel { get; set; } = "Information";
}
