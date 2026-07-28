namespace UnifyBuild.Nuke;

/// <summary>
/// Observability configuration for build metrics and optional telemetry.
/// </summary>
public sealed class ObservabilityConfig
{
    /// <summary>
    /// Whether build metrics collection is enabled. Default: true.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Output format for metrics reports: "json" or "csv". Default: "json".
    /// </summary>
    public string MetricsFormat { get; set; } = "json";

    /// <summary>
    /// Directory for metrics output files. If null, defaults to build/_metrics.
    /// </summary>
    public string? MetricsOutputDir { get; set; }

    /// <summary>
    /// Whether anonymous telemetry collection is enabled. Default: false (opt-in only).
    /// Telemetry data is stored locally and never sent to any remote server.
    /// </summary>
    public bool EnableTelemetry { get; set; } = false;
}
