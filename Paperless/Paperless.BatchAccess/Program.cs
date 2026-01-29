using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paperless.BatchAccess;
using Paperless.Domain.Repositories;
using Paperless.Infrastructure.DependencyInjection;

// Default: run once and exit (manual mode for our code review)
// Optional: run as a scheduled daemon at a configured daily UTC time (e.g. 01:00)
// Reads XML files from an input folder, aggregates daily accesses, persists to PostgreSQL,
// then archives processed files (or moves failed ones to the error folder).

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        // Always load appsettings from the BatchAccess output directory.
        // This is robust regardless of where one starts the app from (working directory).
        var basePath = AppContext.BaseDirectory;
        cfg.SetBasePath(basePath);

        // Required: appsettings.json copied to output directory via .csproj setting
        cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        // Optional override: allow dropping an appsettings.json next to where one starts the app
        cfg.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"), optional: true, reloadOnChange: true);

        // Environment variable overrides
        cfg.AddEnvironmentVariables();
    })
    .ConfigureServices((ctx, services) =>
    {
        // Reuse Infrastructure wiring (DbContext + repositories + options)
        services.AddInfrastructure(ctx.Configuration);

        // Batch-specific services
        services.AddSingleton<AccessLogXmlParser>();
    })
    .Build();

// Logger
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("BatchAccess");

// Read batch config
var config = host.Services.GetRequiredService<IConfiguration>();
var batchSection = config.GetSection("BatchAccess");

var inputFolder = batchSection["InputFolder"] ?? "BatchInput";
var archiveFolder = batchSection["ArchiveFolder"] ?? "BatchArchive";
var errorFolder = batchSection["ErrorFolder"] ?? "BatchError";
var filePattern = batchSection["FilePattern"] ?? "access-*.xml";

// Schedule config (UTC)
var scheduleSection = batchSection.GetSection("Schedule");
var scheduleEnabledConfig = scheduleSection["Enabled"];
var dailyTimeUtcStr = scheduleSection["DailyTimeUtc"] ?? "01:00";

// Determine run mode:
// Manual mode by default
// Daemon mode if "--daemon" passed or config Schedule:Enabled = true
var runAsDaemon =
    args.Any(a => string.Equals(a, "--daemon", StringComparison.OrdinalIgnoreCase))
    || string.Equals(scheduleEnabledConfig, "true", StringComparison.OrdinalIgnoreCase);

// Parse daily UTC time (HH:mm)
if (!TimeOnly.TryParseExact(dailyTimeUtcStr, "HH:mm", out var dailyUtcTime))
{
    throw new FormatException($"BatchAccess:Schedule:DailyTimeUtc must be HH:mm, got '{dailyTimeUtcStr}'.");
}

// Ensure folders exist (once at startup)
Directory.CreateDirectory(inputFolder);
Directory.CreateDirectory(archiveFolder);
Directory.CreateDirectory(errorFolder);

// Log startup info
logger.LogInformation("BatchAccess starting.");
logger.LogInformation("InputFolder: {InputFolder}", Path.GetFullPath(inputFolder));
logger.LogInformation("ArchiveFolder: {ArchiveFolder}", Path.GetFullPath(archiveFolder));
logger.LogInformation("ErrorFolder: {ErrorFolder}", Path.GetFullPath(errorFolder));
logger.LogInformation("FilePattern: {FilePattern}", filePattern);
logger.LogInformation("Mode: {Mode}", runAsDaemon ? "Daemon (scheduled)" : "Manual (run once)");
logger.LogInformation("Daily schedule time (UTC): {Time}", dailyUtcTime.ToString("HH:mm"));

// Resolve required services
var parser = host.Services.GetRequiredService<AccessLogXmlParser>();
var accessRepo = host.Services.GetRequiredService<IDocumentDailyAccessRepository>();

// Manual run-once mode (default)
if (!runAsDaemon)
{
    await ProcessOnceAsync(logger, parser, accessRepo, inputFolder, archiveFolder, errorFolder, filePattern);
    logger.LogInformation("Run-once mode finished.");
    return;
}

// Daemon mode (scheduled daily at configured UTC time)
logger.LogInformation("Daemon mode enabled. Will run daily at {Time} UTC.", dailyUtcTime.ToString("HH:mm"));

while (true)
{
    var nowUtc = DateTime.UtcNow;
    var todayTargetUtc = new DateTime(
        nowUtc.Year, nowUtc.Month, nowUtc.Day,
        dailyUtcTime.Hour, dailyUtcTime.Minute, 0,
        DateTimeKind.Utc);

    var nextRunUtc = todayTargetUtc > nowUtc ? todayTargetUtc : todayTargetUtc.AddDays(1);
    var delay = nextRunUtc - nowUtc;

    logger.LogInformation("Next run scheduled for {NextRun} UTC (in {Delay}).", nextRunUtc, delay);

    await Task.Delay(delay);

    logger.LogInformation("Scheduled run starting at {Now} UTC.", DateTime.UtcNow);
    await ProcessOnceAsync(logger, parser, accessRepo, inputFolder, archiveFolder, errorFolder, filePattern);
    logger.LogInformation("Scheduled run finished at {Now} UTC.", DateTime.UtcNow);
}


// Helper: Process once
static async Task ProcessOnceAsync(
    ILogger logger,
    AccessLogXmlParser parser,
    IDocumentDailyAccessRepository accessRepo,
    string inputFolder,
    string archiveFolder,
    string errorFolder,
    string filePattern)
{
    // Discover files
    var files = Directory.GetFiles(inputFolder, filePattern, SearchOption.TopDirectoryOnly)
        .OrderBy(f => f)
        .ToArray();

    if (files.Length == 0)
    {
        logger.LogInformation("No files found.");
        return;
    }

    logger.LogInformation("Found {Count} file(s):", files.Length);
    foreach (var f in files)
        logger.LogInformation(" - {File}", f);

    // Process each file: parse -> persist -> archive
    foreach (var f in files)
    {
        try
        {
            logger.LogInformation("Processing {File}", f);

            // Parse + aggregate daily counts (UTC day)
            var aggregates = parser.ParseAndAggregate(f);

            // Persist (absolute upsert) for each (DocumentId, DateUtc, AccessType)
            foreach (var kvp in aggregates)
            {
                await accessRepo.UpsertAbsoluteAsync(
                    kvp.Key.DocumentId,
                    kvp.Key.DateUtc,
                    kvp.Key.AccessType,
                    kvp.Value);
            }

            // Archive after successful DB writes (prevents redundant processing)
            var archiveName = $"{Path.GetFileNameWithoutExtension(f)}-{DateTime.UtcNow:yyyyMMddHHmmss}.xml";
            var archivePath = Path.Combine(archiveFolder, archiveName);

            File.Move(f, archivePath);
            logger.LogInformation("Archived {File} -> {ArchivePath}", f, archivePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process file {File}", f);

            // Move failed file into Error folder so it doesn't block future runs
            try
            {
                var errorName = $"{Path.GetFileNameWithoutExtension(f)}-{DateTime.UtcNow:yyyyMMddHHmmss}.xml";
                var errorPath = Path.Combine(errorFolder, errorName);

                File.Move(f, errorPath);
                logger.LogWarning("Moved failed file {File} -> {ErrorPath}", f, errorPath);
            }
            catch (Exception moveEx)
            {
                logger.LogError(moveEx, "Could not move failed file {File} to error folder.", f);
            }
        }
    }
}
