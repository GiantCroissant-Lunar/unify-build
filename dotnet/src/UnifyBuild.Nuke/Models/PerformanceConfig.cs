namespace UnifyBuild.Nuke;

/// <summary>
/// Performance configuration for build caching and change detection.
/// </summary>
public sealed class PerformanceConfig
{
    /// <summary>
    /// Whether local build caching is enabled. Default: false.
    /// </summary>
    public bool EnableCache { get; set; } = false;

    /// <summary>
    /// Directory for local cache storage. If null, defaults to build/_cache.
    /// </summary>
    public string? CacheDir { get; set; }

    /// <summary>
    /// Whether file-based change detection is enabled. Default: true.
    /// </summary>
    public bool EnableChangeDetection { get; set; } = true;

    /// <summary>
    /// URL for distributed cache server. When set, cache entries are uploaded/downloaded
    /// via HTTP PUT/GET using the cache key as the URL path segment.
    /// Example: "https://cache.example.com/unify-build"
    /// </summary>
    public string? DistributedCacheUrl { get; set; }

    /// <summary>
    /// Maximum degree of parallelism for building independent project groups.
    /// Defaults to <see cref="Environment.ProcessorCount"/> when null or &lt;= 0.
    /// </summary>
    public int? MaxParallelism { get; set; }
}
