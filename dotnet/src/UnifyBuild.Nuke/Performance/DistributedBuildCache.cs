using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Nuke.Common.IO;

namespace UnifyBuild.Nuke.Performance;

/// <summary>
/// Extends <see cref="BuildCache"/> with distributed cache support via HTTP.
/// Cache entries are uploaded (PUT) and downloaded (GET) using the cache key as the URL path segment.
/// Falls back to local cache on network failure. Retries transient failures with exponential backoff.
/// </summary>
public sealed class DistributedBuildCache
{
    private readonly BuildCache _localCache;
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly int _maxRetries;

    /// <summary>Total number of cache lookups (hits + misses).</summary>
    public int TotalLookups => CacheHits + CacheMisses;

    /// <summary>Number of cache hits (found in local or distributed cache).</summary>
    public int CacheHits { get; private set; }

    /// <summary>Number of cache misses.</summary>
    public int CacheMisses { get; private set; }

    /// <summary>Number of distributed cache uploads performed.</summary>
    public int Uploads { get; private set; }

    /// <summary>Number of distributed cache downloads performed.</summary>
    public int Downloads { get; private set; }

    /// <summary>
    /// Creates a new <see cref="DistributedBuildCache"/> instance.
    /// </summary>
    /// <param name="localCache">The local cache to use as primary/fallback.</param>
    /// <param name="baseUrl">Base URL for the distributed cache (e.g., "https://cache.example.com/unify-build").</param>
    /// <param name="httpClient">Optional HTTP client (for testing).</param>
    /// <param name="maxRetries">Maximum retry attempts for transient failures. Default: 3.</param>
    public DistributedBuildCache(BuildCache localCache, string baseUrl, HttpClient? httpClient = null, int maxRetries = 3)
    {
        _localCache = localCache ?? throw new ArgumentNullException(nameof(localCache));
        _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
        _httpClient = httpClient ?? new HttpClient();
        _maxRetries = maxRetries;
    }

    /// <summary>
    /// Attempts to retrieve cached build outputs. Checks local cache first,
    /// then falls back to the distributed cache. Downloads are stored locally for future use.
    /// </summary>
    public bool TryGetCached(string cacheKey, AbsolutePath outputDir)
    {
        // Try local cache first
        if (_localCache.TryGetCached(cacheKey, outputDir))
        {
            CacheHits++;
            return true;
        }

        // Try distributed cache
        try
        {
            var downloaded = DownloadWithRetryAsync(cacheKey, outputDir).GetAwaiter().GetResult();
            if (downloaded)
            {
                CacheHits++;
                Downloads++;
                return true;
            }
        }
        catch (Exception ex)
        {
            global::Serilog.Log.Warning("Distributed cache download failed for key {CacheKey}: {Message}", cacheKey, ex.Message);
        }

        CacheMisses++;
        return false;
    }

    /// <summary>
    /// Stores build outputs in both local and distributed cache.
    /// Distributed upload failures are logged but do not prevent local caching.
    /// </summary>
    public void Store(string cacheKey, AbsolutePath outputDir)
    {
        // Always store locally
        _localCache.Store(cacheKey, outputDir);

        // Upload to distributed cache (best-effort)
        try
        {
            UploadWithRetryAsync(cacheKey, outputDir).GetAwaiter().GetResult();
            Uploads++;
        }
        catch (Exception ex)
        {
            global::Serilog.Log.Warning("Distributed cache upload failed for key {CacheKey}: {Message}", cacheKey, ex.Message);
        }
    }

    /// <summary>
    /// Returns a summary of cache statistics.
    /// </summary>
    public string GetStatsSummary()
    {
        var hitRate = TotalLookups > 0 ? (double)CacheHits / TotalLookups * 100 : 0;
        return $"Cache: {CacheHits} hits, {CacheMisses} misses ({hitRate:F1}% hit rate), {Downloads} downloads, {Uploads} uploads";
    }

    private async Task<bool> DownloadWithRetryAsync(string cacheKey, AbsolutePath outputDir)
    {
        var url = $"{_baseUrl}/{cacheKey}";

        for (int attempt = 0; attempt < _maxRetries; attempt++)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return false;

                response.EnsureSuccessStatusCode();

                var outputPath = (string)outputDir;
                if (!Directory.Exists(outputPath))
                    Directory.CreateDirectory(outputPath);

                var tempFile = Path.Combine(Path.GetTempPath(), $"unify-cache-{cacheKey}.tmp");
                try
                {
                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var fileStream = File.Create(tempFile))
                    {
                        await stream.CopyToAsync(fileStream).ConfigureAwait(false);
                    }

                    // Copy downloaded content to output directory
                    File.Copy(tempFile, Path.Combine(outputPath, "cached-output"), overwrite: true);
                }
                finally
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }

                return true;
            }
            catch (HttpRequestException) when (attempt < _maxRetries - 1)
            {
                var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 500);
                Thread.Sleep(delay);
            }
        }

        return false;
    }

    private async Task UploadWithRetryAsync(string cacheKey, AbsolutePath outputDir)
    {
        var url = $"{_baseUrl}/{cacheKey}";
        var outputPath = (string)outputDir;

        if (!Directory.Exists(outputPath))
            return;

        // Upload each file in the output directory
        var files = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(outputPath, file);
            var fileUrl = $"{url}/{relativePath}";

            for (int attempt = 0; attempt < _maxRetries; attempt++)
            {
                try
                {
                    var content = new ByteArrayContent(File.ReadAllBytes(file));
                    using var response = await _httpClient.PutAsync(fileUrl, content).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    break;
                }
                catch (HttpRequestException) when (attempt < _maxRetries - 1)
                {
                    var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 500);
                    Thread.Sleep(delay);
                }
            }
        }
    }
}
