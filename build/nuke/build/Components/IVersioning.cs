using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;

/// <summary>
/// Shared versioning and artifacts root logic for Nuke components.
/// Determines a version string from:
/// 1. BUILD_VERSION environment variable, then
/// 2. Latest git tag (git describe --tags --abbrev=0), then
/// 3. A local fallback "0.0.0-local".
/// </summary>
interface IVersioning : INukeBuild
{
    /// <summary>
    /// Root directory for all build artifacts (defaults to build/_artifacts).
    /// </summary>
    AbsolutePath ArtifactsRoot => RootDirectory / "build" / "_artifacts";

    /// <summary>
    /// Version used for versioned artifact folders.
    /// </summary>
    string ArtifactsVersion => VersioningExtensions.GetArtifactsVersion();

    /// <summary>
    /// Versioned publish directory (build/_artifacts/{version}).
    /// </summary>
    AbsolutePath PublishDirectory => ArtifactsRoot / ArtifactsVersion;

    /// <summary>
    /// Per-version build logs directory (build/_artifacts/{version}/build-logs).
    /// </summary>
    AbsolutePath BuildLogsDirectory => PublishDirectory / "build-logs";
}

static class VersioningExtensions
{
    public static string GetArtifactsVersion()
    {
        // 1) Explicit override via environment variable
        var envVersion = Environment.GetEnvironmentVariable("BUILD_VERSION");
        if (!string.IsNullOrWhiteSpace(envVersion))
            return envVersion.Trim();

        // 2) Try to read the latest git tag (if git is available)
        try
        {
            var process = ProcessTasks.StartProcess(
                "git",
                "describe --tags --abbrev=0",
                logOutput: false,
                logInvocation: false);

            process.AssertWaitForExit();

            if (process.ExitCode == 0)
            {
                var tag = process.Output?.FirstOrDefault()?.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(tag))
                    return tag;
            }
        }
        catch
        {
            // Ignore any git errors and fall back to local version
        }

        // 3) Fallback for local/dev environments
        return "0.0.0-local";
    }
}
