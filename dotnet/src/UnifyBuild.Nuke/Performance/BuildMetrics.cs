using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnifyBuild.Nuke.Performance;

/// <summary>
/// Serializable report generated from a completed build's metrics.
/// </summary>
public sealed record BuildMetricsReport(
    DateTimeOffset Timestamp,
    TimeSpan TotalDuration,
    Dictionary<string, TimeSpan> TargetDurations,
    Dictionary<string, TimeSpan> ProjectDurations,
    int CacheHits,
    int CacheMisses,
    double CacheHitRate,
    bool Success
);

/// <summary>
/// Tracks build performance metrics including per-target durations,
/// per-project compilation times, and cache hit/miss rates.
/// Thread-safe for concurrent target execution.
/// </summary>
/// <remarks>
/// Usage:
/// <code>
/// var metrics = new BuildMetrics();
/// metrics.StartBuild();
///
/// metrics.StartTarget("Compile");
/// // ... compile ...
/// metrics.EndTarget("Compile");
///
/// metrics.RecordCacheHit();
///
/// metrics.EndBuild(success: true);
/// metrics.ExportJson("build/metrics.json");
/// </code>
/// </remarks>
public sealed class BuildMetrics
{
    private readonly ConcurrentDictionary<string, Stopwatch> _activeTargets = new();
    private readonly ConcurrentDictionary<string, TimeSpan> _targetDurations = new();
    private readonly ConcurrentDictionary<string, Stopwatch> _activeProjects = new();
    private readonly ConcurrentDictionary<string, TimeSpan> _projectDurations = new();

    private readonly Stopwatch _buildStopwatch = new();
    private DateTimeOffset _buildStartTime;
    private int _cacheHits;
    private int _cacheMisses;
    private bool _success;

    /// <summary>Total build duration. Only valid after <see cref="EndBuild"/> is called.</summary>
    public TimeSpan TotalDuration => _buildStopwatch.Elapsed;

    /// <summary>Completed target durations (target name → elapsed time).</summary>
    public IReadOnlyDictionary<string, TimeSpan> TargetDurations => _targetDurations;

    /// <summary>Completed project compilation durations (project name → elapsed time).</summary>
    public IReadOnlyDictionary<string, TimeSpan> ProjectDurations => _projectDurations;

    /// <summary>Number of cache hits recorded.</summary>
    public int CacheHits => _cacheHits;

    /// <summary>Number of cache misses recorded.</summary>
    public int CacheMisses => _cacheMisses;

    /// <summary>Total cache lookups (hits + misses).</summary>
    public int TotalCacheLookups => _cacheHits + _cacheMisses;

    /// <summary>Cache hit rate as a value between 0.0 and 1.0. Returns 0 if no lookups recorded.</summary>
    public double CacheHitRate => TotalCacheLookups == 0 ? 0.0 : (double)_cacheHits / TotalCacheLookups;

    /// <summary>Whether the build succeeded. Only valid after <see cref="EndBuild"/> is called.</summary>
    public bool Success => _success;

    // ── Build lifecycle ──────────────────────────────────────────────

    /// <summary>Marks the start of the overall build.</summary>
    public void StartBuild()
    {
        _buildStartTime = DateTimeOffset.UtcNow;
        _buildStopwatch.Restart();
    }

    /// <summary>Marks the end of the overall build.</summary>
    /// <param name="success">Whether the build completed successfully.</param>
    public void EndBuild(bool success = true)
    {
        _buildStopwatch.Stop();
        _success = success;
    }

    // ── Target tracking ──────────────────────────────────────────────

    /// <summary>Starts timing a build target.</summary>
    /// <param name="name">The target name (e.g. "Compile", "Pack").</param>
    public void StartTarget(string name)
    {
        var sw = new Stopwatch();
        sw.Start();
        _activeTargets[name] = sw;
    }

    /// <summary>Stops timing a build target and records its duration.</summary>
    /// <param name="name">The target name previously passed to <see cref="StartTarget"/>.</param>
    public void EndTarget(string name)
    {
        if (_activeTargets.TryRemove(name, out var sw))
        {
            sw.Stop();
            _targetDurations[name] = sw.Elapsed;
        }
    }

    // ── Project tracking ─────────────────────────────────────────────

    /// <summary>Starts timing a project compilation.</summary>
    /// <param name="name">The project name (e.g. "MyApp.csproj").</param>
    public void StartProject(string name)
    {
        var sw = new Stopwatch();
        sw.Start();
        _activeProjects[name] = sw;
    }

    /// <summary>Stops timing a project compilation and records its duration.</summary>
    /// <param name="name">The project name previously passed to <see cref="StartProject"/>.</param>
    public void EndProject(string name)
    {
        if (_activeProjects.TryRemove(name, out var sw))
        {
            sw.Stop();
            _projectDurations[name] = sw.Elapsed;
        }
    }

    // ── Cache tracking ───────────────────────────────────────────────

    /// <summary>Records a cache hit.</summary>
    public void RecordCacheHit() => Interlocked.Increment(ref _cacheHits);

    /// <summary>Records a cache miss.</summary>
    public void RecordCacheMiss() => Interlocked.Increment(ref _cacheMisses);

    // ── Reporting ────────────────────────────────────────────────────

    /// <summary>
    /// Creates a serializable <see cref="BuildMetricsReport"/> snapshot of the current metrics.
    /// </summary>
    public BuildMetricsReport ToReport()
    {
        return new BuildMetricsReport(
            Timestamp: _buildStartTime,
            TotalDuration: _buildStopwatch.Elapsed,
            TargetDurations: new Dictionary<string, TimeSpan>(_targetDurations),
            ProjectDurations: new Dictionary<string, TimeSpan>(_projectDurations),
            CacheHits: _cacheHits,
            CacheMisses: _cacheMisses,
            CacheHitRate: CacheHitRate,
            Success: _success
        );
    }

    /// <summary>
    /// Serializes the metrics report to a JSON file at <paramref name="outputPath"/>.
    /// Creates the parent directory if it does not exist.
    /// </summary>
    public void ExportJson(string outputPath)
    {
        EnsureDirectory(outputPath);

        var report = ToReport();
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new TimeSpanJsonConverter() },
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(report, options);
        File.WriteAllText(outputPath, json);
    }

    /// <summary>
    /// Serializes the metrics report to a CSV file at <paramref name="outputPath"/>.
    /// The CSV contains one header row and one data row for the build summary,
    /// followed by sections for target and project durations.
    /// Creates the parent directory if it does not exist.
    /// </summary>
    public void ExportCsv(string outputPath)
    {
        EnsureDirectory(outputPath);

        var report = ToReport();
        var sb = new StringBuilder();

        // Build summary section
        sb.AppendLine("Section,Key,Value");
        sb.AppendLine(CsvLine("Build", "Timestamp", report.Timestamp.ToString("o")));
        sb.AppendLine(CsvLine("Build", "TotalDuration", FormatDuration(report.TotalDuration)));
        sb.AppendLine(CsvLine("Build", "Success", report.Success.ToString()));
        sb.AppendLine(CsvLine("Build", "CacheHits", report.CacheHits.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(CsvLine("Build", "CacheMisses", report.CacheMisses.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(CsvLine("Build", "CacheHitRate", report.CacheHitRate.ToString("F4", CultureInfo.InvariantCulture)));

        // Target durations
        foreach (var (name, duration) in report.TargetDurations.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            sb.AppendLine(CsvLine("Target", name, FormatDuration(duration)));
        }

        // Project durations
        foreach (var (name, duration) in report.ProjectDurations.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            sb.AppendLine(CsvLine("Project", name, FormatDuration(duration)));
        }

        File.WriteAllText(outputPath, sb.ToString());
    }

    /// <summary>
    /// Returns a human-readable summary string suitable for console output.
    /// </summary>
    public string GetSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔══════════════════════════════════════╗");
        sb.AppendLine("║         Build Metrics Summary        ║");
        sb.AppendLine("╠══════════════════════════════════════╣");
        sb.AppendLine($"  Status:       {(_success ? "✓ Success" : "✗ Failed")}");
        sb.AppendLine($"  Duration:     {FormatDuration(_buildStopwatch.Elapsed)}");
        sb.AppendLine($"  Cache:        {_cacheHits} hits / {_cacheMisses} misses ({CacheHitRate:P0})");

        if (_targetDurations.Count > 0)
        {
            sb.AppendLine("  ──────────────────────────────────");
            sb.AppendLine("  Targets:");
            foreach (var (name, duration) in _targetDurations.OrderByDescending(kv => kv.Value))
            {
                sb.AppendLine($"    {name,-20} {FormatDuration(duration)}");
            }
        }

        if (_projectDurations.Count > 0)
        {
            sb.AppendLine("  ──────────────────────────────────");
            sb.AppendLine("  Projects:");
            foreach (var (name, duration) in _projectDurations.OrderByDescending(kv => kv.Value))
            {
                sb.AppendLine($"    {name,-20} {FormatDuration(duration)}");
            }
        }

        sb.AppendLine("╚══════════════════════════════════════╝");
        return sb.ToString();
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static string FormatDuration(TimeSpan ts) =>
        ts.TotalMinutes >= 1
            ? $"{ts.Minutes}m {ts.Seconds:D2}.{ts.Milliseconds:D3}s"
            : $"{ts.TotalSeconds:F3}s";

    private static string CsvLine(string section, string key, string value) =>
        $"{EscapeCsv(section)},{EscapeCsv(key)},{EscapeCsv(value)}";

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static void EnsureDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }

    /// <summary>
    /// Custom JSON converter for <see cref="TimeSpan"/> — serializes as ISO 8601 duration
    /// and also accepts the total-seconds numeric format for round-trip flexibility.
    /// </summary>
    internal sealed class TimeSpanJsonConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString()!;
                return TimeSpan.Parse(str, CultureInfo.InvariantCulture);
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                return TimeSpan.FromSeconds(reader.GetDouble());
            }

            throw new JsonException($"Cannot convert token type {reader.TokenType} to TimeSpan.");
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        {
            // Use the invariant round-trip format: "d.hh:mm:ss.fffffff"
            writer.WriteStringValue(value.ToString("c", CultureInfo.InvariantCulture));
        }
    }
}
