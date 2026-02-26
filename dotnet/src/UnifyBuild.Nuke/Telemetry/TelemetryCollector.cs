using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnifyBuild.Nuke.Telemetry;

/// <summary>
/// Anonymous telemetry record stored locally per build.
/// Contains only non-identifiable build performance data.
/// </summary>
public sealed record TelemetryRecord(
    /// <summary>Random GUID generated per build session — not tied to any user identity.</summary>
    string SessionId,
    /// <summary>UTC timestamp of the build.</summary>
    DateTimeOffset Timestamp,
    /// <summary>Total build duration in milliseconds.</summary>
    double BuildDurationMs,
    /// <summary>Number of build targets executed.</summary>
    int TargetCount,
    /// <summary>Cache hit rate as a value between 0.0 and 1.0.</summary>
    double CacheHitRate,
    /// <summary>Operating system description (e.g., "Windows 10", "Ubuntu 22.04").</summary>
    string OperatingSystem,
    /// <summary>.NET SDK version used for the build.</summary>
    string DotNetSdkVersion,
    /// <summary>Whether the build succeeded.</summary>
    bool Success
);

/// <summary>
/// Collects anonymous, local-only telemetry data when explicitly opted in.
/// Telemetry is disabled by default and must be enabled via
/// <c>observability.enableTelemetry = true</c> in <c>build.config.json</c>.
/// Data is stored as JSON files in <c>build/_telemetry/</c> and is never sent remotely.
/// </summary>
public static class TelemetryCollector
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Records a telemetry snapshot for the current build if telemetry is enabled.
    /// </summary>
    /// <param name="enabled">Whether telemetry is enabled (from <see cref="ObservabilityConfig.EnableTelemetry"/>).</param>
    /// <param name="buildDuration">Total build duration.</param>
    /// <param name="targetCount">Number of targets executed.</param>
    /// <param name="cacheHitRate">Cache hit rate (0.0–1.0).</param>
    /// <param name="success">Whether the build succeeded.</param>
    /// <param name="outputDir">Base output directory. Telemetry files are written to <c>{outputDir}/_telemetry/</c>.</param>
    public static void Record(
        bool enabled,
        TimeSpan buildDuration,
        int targetCount,
        double cacheHitRate,
        bool success,
        string outputDir)
    {
        if (!enabled)
            return;

        var record = new TelemetryRecord(
            SessionId: Guid.NewGuid().ToString("N"),
            Timestamp: DateTimeOffset.UtcNow,
            BuildDurationMs: buildDuration.TotalMilliseconds,
            TargetCount: targetCount,
            CacheHitRate: cacheHitRate,
            OperatingSystem: RuntimeInformation.OSDescription,
            DotNetSdkVersion: Environment.Version.ToString(),
            Success: success
        );

        var telemetryDir = Path.Combine(outputDir, "_telemetry");
        Directory.CreateDirectory(telemetryDir);

        var fileName = $"telemetry-{record.Timestamp:yyyyMMdd-HHmmss}-{record.SessionId[..8]}.json";
        var filePath = Path.Combine(telemetryDir, fileName);

        var json = JsonSerializer.Serialize(record, s_jsonOptions);
        File.WriteAllText(filePath, json);
    }
}
