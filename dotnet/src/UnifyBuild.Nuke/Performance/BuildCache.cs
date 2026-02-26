using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Nuke.Common.IO;

namespace UnifyBuild.Nuke.Performance;

/// <summary>
/// Provides local build output caching based on content hashing.
/// Cache keys are computed from the project file, source files, and build configuration.
/// Cached outputs are stored under <c>build/_cache/{cacheKey}/</c> by default.
/// </summary>
public sealed class BuildCache
{
    private readonly AbsolutePath _cacheDir;

    /// <summary>
    /// Creates a new <see cref="BuildCache"/> instance.
    /// </summary>
    /// <param name="cacheDir">
    /// Optional override for the cache directory. If <c>null</c>, defaults to
    /// <c>build/_cache/</c> relative to the repository root inferred from the first usage.
    /// </param>
    public BuildCache(AbsolutePath? cacheDir = null)
    {
        _cacheDir = cacheDir ?? (AbsolutePath)Path.Combine(Directory.GetCurrentDirectory(), "build", "_cache");
    }

    /// <summary>
    /// Computes a deterministic cache key by hashing the project file content,
    /// all source files in the project directory, and relevant build configuration properties.
    /// </summary>
    /// <param name="projectPath">Absolute path to the .csproj file.</param>
    /// <param name="config">The build context containing version and configuration info.</param>
    /// <returns>A hex-encoded SHA256 hash string.</returns>
    public string ComputeCacheKey(AbsolutePath projectPath, BuildContext config)
    {
        using var sha256 = SHA256.Create();
        using var stream = new CryptoStream(Stream.Null, sha256, CryptoStreamMode.Write);

        // 1. Hash the project file content
        HashFileContent(stream, (string)projectPath);

        // 2. Hash all source files in the project directory (sorted for determinism)
        var projectDir = (AbsolutePath)Path.GetDirectoryName((string)projectPath)!;
        var sourceFiles = ChangeDetection.EnumerateSourceFiles(projectDir)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        foreach (var file in sourceFiles)
        {
            HashFileContent(stream, file);
        }

        // 3. Hash relevant BuildContext properties
        var configString = $"{config.Version ?? string.Empty}|{config.ArtifactsVersion ?? string.Empty}";
        var configBytes = Encoding.UTF8.GetBytes(configString);
        stream.Write(configBytes, 0, configBytes.Length);

        stream.FlushFinalBlock();
        return Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
    }

    /// <summary>
    /// Attempts to retrieve cached build outputs for the given cache key.
    /// If a cache entry exists, all files are copied to <paramref name="outputDir"/>.
    /// </summary>
    /// <param name="cacheKey">The cache key (hex hash) to look up.</param>
    /// <param name="outputDir">The directory to copy cached files into.</param>
    /// <returns><c>true</c> if the cache entry was found and restored; <c>false</c> otherwise.</returns>
    public bool TryGetCached(string cacheKey, AbsolutePath outputDir)
    {
        var cacheEntryDir = Path.Combine((string)_cacheDir, cacheKey);

        if (!Directory.Exists(cacheEntryDir))
            return false;

        var cachedFiles = Directory.GetFiles(cacheEntryDir, "*", SearchOption.AllDirectories);
        if (cachedFiles.Length == 0)
            return false;

        var outputPath = (string)outputDir;
        if (!Directory.Exists(outputPath))
            Directory.CreateDirectory(outputPath);

        foreach (var cachedFile in cachedFiles)
        {
            var relativePath = Path.GetRelativePath(cacheEntryDir, cachedFile);
            var destFile = Path.Combine(outputPath, relativePath);
            var destDir = Path.GetDirectoryName(destFile);

            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            File.Copy(cachedFile, destFile, overwrite: true);
        }

        return true;
    }

    /// <summary>
    /// Stores the build outputs from <paramref name="outputDir"/> into the cache
    /// under the given <paramref name="cacheKey"/>. Overwrites any existing cache entry.
    /// </summary>
    /// <param name="cacheKey">The cache key (hex hash) to store under.</param>
    /// <param name="outputDir">The directory containing build outputs to cache.</param>
    public void Store(string cacheKey, AbsolutePath outputDir)
    {
        var cacheEntryDir = Path.Combine((string)_cacheDir, cacheKey);

        // Overwrite existing cache entry
        if (Directory.Exists(cacheEntryDir))
            Directory.Delete(cacheEntryDir, recursive: true);

        Directory.CreateDirectory(cacheEntryDir);

        var outputPath = (string)outputDir;
        if (!Directory.Exists(outputPath))
            return;

        var outputFiles = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories);

        foreach (var file in outputFiles)
        {
            var relativePath = Path.GetRelativePath(outputPath, file);
            var destFile = Path.Combine(cacheEntryDir, relativePath);
            var destDir = Path.GetDirectoryName(destFile);

            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            File.Copy(file, destFile, overwrite: true);
        }
    }

    private static void HashFileContent(CryptoStream stream, string filePath)
    {
        if (!File.Exists(filePath))
            return;

        var bytes = File.ReadAllBytes(filePath);
        stream.Write(bytes, 0, bytes.Length);
    }
}
