using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UnifyBuild.Nuke.PackageManagement;

/// <summary>
/// Applies retention policies to local NuGet feeds by removing old package versions.
/// </summary>
public sealed class RetentionPolicy
{
    /// <summary>
    /// Result of a retention cleanup operation.
    /// </summary>
    public sealed record RetentionResult(int PackagesRemoved, IReadOnlyList<string> RemovedFiles);

    /// <summary>
    /// Applies the retention policy to the configured local feed path.
    /// Removes packages exceeding MaxVersions per package ID and packages older than MaxAgeDays.
    /// </summary>
    public RetentionResult Apply(RetentionPolicyConfig config)
    {
        var removedFiles = new List<string>();

        if (string.IsNullOrEmpty(config.LocalFeedPath))
        {
            Serilog.Log.Warning("No local feed path configured for retention policy");
            return new RetentionResult(0, removedFiles);
        }

        if (!Directory.Exists(config.LocalFeedPath))
        {
            Serilog.Log.Warning("Local feed path does not exist: {Path}", config.LocalFeedPath);
            return new RetentionResult(0, removedFiles);
        }

        var nupkgFiles = Directory.GetFiles(config.LocalFeedPath, "*.nupkg", SearchOption.AllDirectories);

        if (nupkgFiles.Length == 0)
        {
            Serilog.Log.Information("No packages found in local feed at {Path}", config.LocalFeedPath);
            return new RetentionResult(0, removedFiles);
        }

        // Remove packages older than MaxAgeDays
        if (config.MaxAgeDays.HasValue && config.MaxAgeDays.Value > 0)
        {
            var cutoff = DateTime.UtcNow.AddDays(-config.MaxAgeDays.Value);
            var oldFiles = nupkgFiles
                .Where(f => File.GetLastWriteTimeUtc(f) < cutoff)
                .ToList();

            foreach (var file in oldFiles)
            {
                TryDeletePackage(file, removedFiles);
            }
        }

        // Remove excess versions per package ID (keep newest MaxVersions)
        if (config.MaxVersions.HasValue && config.MaxVersions.Value > 0)
        {
            // Re-scan after age-based removal
            nupkgFiles = Directory.GetFiles(config.LocalFeedPath, "*.nupkg", SearchOption.AllDirectories)
                .Where(f => !removedFiles.Contains(f))
                .ToArray();

            // Group by package ID (filename without version)
            var groups = nupkgFiles
                .GroupBy(f => ExtractPackageId(Path.GetFileNameWithoutExtension(f)), StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                var sorted = group
                    .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
                    .ToList();

                if (sorted.Count > config.MaxVersions.Value)
                {
                    var toRemove = sorted.Skip(config.MaxVersions.Value);
                    foreach (var file in toRemove)
                    {
                        TryDeletePackage(file, removedFiles);
                    }
                }
            }
        }

        Serilog.Log.Information("Retention policy removed {Count} packages from {Path}",
            removedFiles.Count, config.LocalFeedPath);

        return new RetentionResult(removedFiles.Count, removedFiles);
    }

    private static void TryDeletePackage(string filePath, List<string> removedFiles)
    {
        try
        {
            File.Delete(filePath);
            removedFiles.Add(filePath);
            Serilog.Log.Debug("Removed package: {File}", Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning("Failed to delete {File}: {Error}", filePath, ex.Message);
        }
    }

    /// <summary>
    /// Extracts the package ID from a .nupkg filename by removing the version suffix.
    /// E.g., "MyPackage.1.2.3" → "MyPackage"
    /// </summary>
    internal static string ExtractPackageId(string fileNameWithoutExtension)
    {
        // NuGet package filenames follow the pattern: {PackageId}.{Version}
        // Version starts with a digit after a dot, e.g., "MyPackage.1.2.3"
        var parts = fileNameWithoutExtension.Split('.');
        var idParts = new List<string>();

        foreach (var part in parts)
        {
            if (part.Length > 0 && char.IsDigit(part[0]))
                break;
            idParts.Add(part);
        }

        return idParts.Count > 0 ? string.Join(".", idParts) : fileNameWithoutExtension;
    }
}
