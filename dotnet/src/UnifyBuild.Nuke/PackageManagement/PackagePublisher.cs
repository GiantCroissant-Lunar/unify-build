using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nuke.Common.Tooling;

namespace UnifyBuild.Nuke.PackageManagement;

/// <summary>
/// Publishes NuGet packages to multiple registries with retry logic.
/// </summary>
public sealed class PackagePublisher
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Result of a publish operation for a single registry.
    /// </summary>
    public sealed record PublishResult(string RegistryName, string PackagePath, bool Success, string? Error = null);

    /// <summary>
    /// Pushes all .nupkg files in the given directory to all configured registries.
    /// </summary>
    public IReadOnlyList<PublishResult> PushToRegistries(
        string packageDirectory,
        NuGetRegistryConfig[] registries)
    {
        var results = new List<PublishResult>();
        var packages = Directory.GetFiles(packageDirectory, "*.nupkg")
            .Where(p => !p.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (packages.Length == 0)
        {
            Serilog.Log.Warning("No .nupkg files found in {Directory}", packageDirectory);
            return results;
        }

        foreach (var registry in registries)
        {
            var apiKey = !string.IsNullOrEmpty(registry.ApiKeyEnvVar)
                ? Environment.GetEnvironmentVariable(registry.ApiKeyEnvVar)
                : null;

            if (string.IsNullOrEmpty(apiKey))
            {
                Serilog.Log.Warning(
                    "API key not found for registry '{Registry}' (env var: {EnvVar}), skipping",
                    registry.Name, registry.ApiKeyEnvVar);
                foreach (var pkg in packages)
                    results.Add(new PublishResult(registry.Name, pkg, false, "API key not configured"));
                continue;
            }

            foreach (var package in packages)
            {
                var result = PushWithRetry(package, registry, apiKey);
                results.Add(result);
            }
        }

        return results;
    }

    private PublishResult PushWithRetry(string packagePath, NuGetRegistryConfig registry, string apiKey)
    {
        var delay = InitialDelay;

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                Serilog.Log.Information(
                    "Pushing {Package} to {Registry} (attempt {Attempt}/{Max})",
                    Path.GetFileName(packagePath), registry.Name, attempt, MaxRetries);

                var arguments = $"nuget push \"{packagePath}\" --source \"{registry.Url}\" --api-key \"{apiKey}\" --skip-duplicate";

                ProcessTasks.StartProcess("dotnet", arguments)
                    .AssertZeroExitCode();

                Serilog.Log.Information("Successfully pushed {Package} to {Registry}",
                    Path.GetFileName(packagePath), registry.Name);

                return new PublishResult(registry.Name, packagePath, true);
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                Serilog.Log.Warning(
                    "Push attempt {Attempt} failed for {Registry}: {Error}. Retrying in {Delay}s...",
                    attempt, registry.Name, ex.Message, delay.TotalSeconds);

                System.Threading.Thread.Sleep(delay);
                delay = TimeSpan.FromTicks(delay.Ticks * 2); // exponential backoff
            }
            catch (Exception ex)
            {
                Serilog.Log.Error("Failed to push {Package} to {Registry} after {Max} attempts: {Error}",
                    Path.GetFileName(packagePath), registry.Name, MaxRetries, ex.Message);

                return new PublishResult(registry.Name, packagePath, false, ex.Message);
            }
        }

        // Should not reach here, but just in case
        return new PublishResult(registry.Name, packagePath, false, "Unexpected failure");
    }
}
