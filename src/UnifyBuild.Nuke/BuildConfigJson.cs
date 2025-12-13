using System;
using System.Collections.Generic;
using System.Text.Json;
using Nuke.Common.IO;

namespace UnifyBuild.Nuke;

public sealed class BuildJsonConfig
{
    public string HostsDir { get; set; } = "project/hosts";
    public string PluginsDir { get; set; } = "project/plugins";
    public string? Solution { get; set; } = "project/MungBean.SkiaSharp.sln";

    /// <summary>
    /// Directory containing contract/abstraction projects to be packed as NuGet packages.
    /// </summary>
    public string? ContractsDir { get; set; }

    /// <summary>
    /// Output directory for NuGet packages. If null, defaults to build/_artifacts/{version}/nuget.
    /// </summary>
    public string? NuGetOutputDir { get; set; }

    /// <summary>
    /// Explicit version to use. If null, VersionEnv and common GitVersion env vars are consulted.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Optional artifacts version to use for build/_artifacts/{ArtifactsVersion} layout.
    /// If null, falls back to Version or to the MSBuild defaults (usually "local").
    /// </summary>
    public string? ArtifactsVersion { get; set; }

    /// <summary>
    /// Name of environment variable to read version from. Defaults to "Version".
    /// </summary>
    public string? VersionEnv { get; set; } = "Version";

    /// <summary>
    /// Optional lists of project names (without .csproj) to include/exclude for hosts and plugins.
    /// If an include list is null/empty, all projects are considered included by default.
    /// </summary>
    public string[]? IncludeHosts { get; set; }
    public string[]? ExcludeHosts { get; set; }
    public string[]? IncludePlugins { get; set; }
    public string[]? ExcludePlugins { get; set; }

    /// <summary>
    /// Contract project names to include for packing. If empty, all in ContractsDir are included.
    /// </summary>
    public string[]? IncludeContracts { get; set; }

    /// <summary>
    /// Contract project names to exclude from packing.
    /// </summary>
    public string[]? ExcludeContracts { get; set; }

    public string[]? CompileProjects { get; set; }

    /// <summary>
    /// Additional MSBuild properties to pass during pack operations.
    /// Example: { "UseDevelopmentReferences": "false" }
    /// </summary>
    public Dictionary<string, string>? PackProperties { get; set; }
}

public static class BuildContextLoader
{
    public static BuildContext FromJson(AbsolutePath repoRoot, string configFile = "build.config.json")
    {
        // Support both root-level and build/ directory configs
        var path = repoRoot / configFile;
        if (!System.IO.File.Exists(path))
        {
            var buildDirPath = repoRoot / "build" / configFile;
            if (System.IO.File.Exists(buildDirPath))
                path = buildDirPath;
        }
        if (!System.IO.File.Exists(path))
            throw new InvalidOperationException($"Build config file '{path}' not found.");

        var json = System.IO.File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var cfg = JsonSerializer.Deserialize<BuildJsonConfig>(json, options)
                  ?? throw new InvalidOperationException($"Failed to parse build config '{path}'.");

        var version = cfg.Version
                      ?? GetEnv(cfg.VersionEnv)
                      ?? GetEnv("GITVERSION_MAJORMINORPATCH");

        var artifactsVersion = cfg.ArtifactsVersion ?? version;

        // Compute default NuGet output directory if not specified
        AbsolutePath? nugetOutputDir = null;
        if (cfg.NuGetOutputDir is not null)
        {
            nugetOutputDir = repoRoot / cfg.NuGetOutputDir;
        }
        else if (artifactsVersion is not null)
        {
            nugetOutputDir = repoRoot / "build" / "_artifacts" / artifactsVersion / "nuget";
        }

        return new BuildContext
        {
            RepoRoot   = repoRoot,
            HostsDir   = repoRoot / cfg.HostsDir,
            PluginsDir = repoRoot / cfg.PluginsDir,
            Solution   = cfg.Solution is null ? null : repoRoot / cfg.Solution,
            ContractsDir = cfg.ContractsDir is null ? null : repoRoot / cfg.ContractsDir,
            NuGetOutputDir = nugetOutputDir,
            Version    = version,
            ArtifactsVersion = artifactsVersion,
            IncludeHosts   = cfg.IncludeHosts   ?? Array.Empty<string>(),
            ExcludeHosts   = cfg.ExcludeHosts   ?? Array.Empty<string>(),
            IncludePlugins = cfg.IncludePlugins ?? Array.Empty<string>(),
            ExcludePlugins = cfg.ExcludePlugins ?? Array.Empty<string>(),
            IncludeContracts = cfg.IncludeContracts ?? Array.Empty<string>(),
            ExcludeContracts = cfg.ExcludeContracts ?? Array.Empty<string>(),
            CompileProjects = cfg.CompileProjects ?? Array.Empty<string>(),
            PackProperties = cfg.PackProperties ?? new Dictionary<string, string>()
        };
    }

    private static string? GetEnv(string? name)
        => string.IsNullOrWhiteSpace(name) ? null : Environment.GetEnvironmentVariable(name);
}
