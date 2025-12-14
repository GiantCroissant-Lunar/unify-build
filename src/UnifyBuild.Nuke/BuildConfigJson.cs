using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Nuke.Common.IO;

namespace UnifyBuild.Nuke;

/// <summary>
/// Generic build configuration schema using flexible project groups.
/// Replaces domain-specific terminology (HostsDir, PluginsDir, ContractsDir)
/// with architecture-agnostic project groups organized by build action.
/// </summary>
public sealed class BuildJsonConfig
{
    /// <summary>
    /// Explicit version to use. If null, VersionEnv and common GitVersion env vars are consulted.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Name of environment variable to read version from. Defaults to "Version".
    /// </summary>
    public string? VersionEnv { get; set; } = "Version";

    /// <summary>
    /// Optional artifacts version to use for build/_artifacts/{ArtifactsVersion} layout.
    /// If null, falls back to Version or to the MSBuild defaults (usually "local").
    /// </summary>
    public string? ArtifactsVersion { get; set; }

    /// <summary>
    /// Optional path to solution file (e.g., "src/MySolution.sln").
    /// </summary>
    public string? Solution { get; set; }

    /// <summary>
    /// Generic project groups organized by purpose (e.g., "executables", "libraries", "packages").
    /// Each group defines a source directory, action (publish/pack/compile), and include/exclude filters.
    /// </summary>
    public Dictionary<string, ProjectGroup>? ProjectGroups { get; set; }

    /// <summary>
    /// Explicit project paths to compile (fallback for edge cases not covered by groups).
    /// </summary>
    public string[]? CompileProjects { get; set; }

    /// <summary>
    /// Explicit project paths to publish (fallback for edge cases not covered by groups).
    /// </summary>
    public string[]? PublishProjects { get; set; }

    /// <summary>
    /// Explicit project paths to pack (fallback for edge cases not covered by groups).
    /// </summary>
    public string[]? PackProjects { get; set; }

    /// <summary>
    /// Output directory for NuGet packages. If null, defaults to build/_artifacts/{version}/nuget.
    /// </summary>
    public string? NuGetOutputDir { get; set; }

    /// <summary>
    /// Output directory for published artifacts. If null, defaults to build/_artifacts/{version}.
    /// </summary>
    public string? PublishOutputDir { get; set; }

    /// <summary>
    /// Additional MSBuild properties to pass during pack operations.
    /// Example: { "UseDevelopmentReferences": "false" }
    /// </summary>
    public Dictionary<string, string>? PackProperties { get; set; }

    // Local NuGet feed sync settings (carried over from v1)
    public bool SyncLocalNugetFeed { get; set; } = false;
    public string? LocalNugetFeedRoot { get; set; }
    public string? LocalNugetFeedFlatSubdir { get; set; } = "flat";
    public string? LocalNugetFeedHierarchicalSubdir { get; set; } = "hierarchical";
    public string? LocalNugetFeedBaseUrl { get; set; }
}

/// <summary>
/// Represents a group of related projects with a common build action.
/// </summary>
public sealed class ProjectGroup
{
    /// <summary>
    /// Directory containing projects for this group (e.g., "src/apps", "src/libs", "project/plugins").
    /// </summary>
    public string SourceDir { get; set; } = string.Empty;

    /// <summary>
    /// Build action to perform: "publish" (executables/runtime libs), "pack" (NuGet packages), "compile" (build only).
    /// </summary>
    public string Action { get; set; } = "compile";

    /// <summary>
    /// Project names to include (without .csproj extension). If null/empty, all projects in SourceDir are included.
    /// </summary>
    public string[]? Include { get; set; }

    /// <summary>
    /// Project names to exclude (without .csproj extension).
    /// </summary>
    public string[]? Exclude { get; set; }

    /// <summary>
    /// Optional: Override output directory for this group. If null, uses global output directories.
    /// </summary>
    public string? OutputDir { get; set; }

    /// <summary>
    /// Optional: Additional MSBuild properties specific to this group.
    /// </summary>
    public Dictionary<string, string>? Properties { get; set; }
}

/// <summary>
/// Loader for build configuration using the generic project groups schema.
/// </summary>
public static class BuildContextLoader
{
    /// <summary>
    /// Load build configuration from JSON file.
    /// </summary>
    /// <param name="repoRoot">Repository root directory</param>
    /// <param name="configFile">Config file name (default: "build.config.json")</param>
    /// <returns>BuildContext representing the configuration</returns>
    public static BuildContext FromJson(AbsolutePath repoRoot, string configFile = "build.config.json")
    {
        // Support both root-level and build/ directory configs
        var path = repoRoot / configFile;
        if (!File.Exists(path))
        {
            var buildDirPath = repoRoot / "build" / configFile;
            if (File.Exists(buildDirPath))
                path = buildDirPath;
        }
        if (!File.Exists(path))
            throw new InvalidOperationException($"Build config file '{path}' not found.");

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        if (!json.Contains("\"projectGroups\"", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Build config must use 'projectGroups' schema. "
                + "See docs/rfcs/rfc-0001-generic-build-schema.md for schema documentation.");
        }

        global::Serilog.Log.Information("Loading build configuration");
        return LoadConfig(repoRoot, json, options);
    }

    private static BuildContext LoadConfig(AbsolutePath repoRoot, string json, JsonSerializerOptions options)
    {
        var cfg = JsonSerializer.Deserialize<BuildJsonConfig>(json, options)
                  ?? throw new InvalidOperationException("Failed to parse build config.");

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

        // Map ProjectGroups to v1-style properties for backward compatibility
        var (hostsDir, pluginsDir, contractsDir, includeHosts, excludeHosts, includePlugins, excludePlugins, includeContracts, excludeContracts)
            = MapProjectGroupsToV1Properties(cfg, repoRoot);

        // Add explicit project paths
        var compileProjects = new List<string>(cfg.CompileProjects ?? Array.Empty<string>());
        var publishProjects = new List<string>(cfg.PublishProjects ?? Array.Empty<string>());
        var packProjects = new List<string>(cfg.PackProjects ?? Array.Empty<string>());

        // Extract projects from groups
        if (cfg.ProjectGroups is not null)
        {
            foreach (var (groupName, group) in cfg.ProjectGroups)
            {
                var projectPaths = DiscoverProjectsInGroup(repoRoot, group);

                switch (group.Action.ToLowerInvariant())
                {
                    case "publish":
                        publishProjects.AddRange(projectPaths);
                        break;
                    case "pack":
                        packProjects.AddRange(projectPaths);
                        break;
                    case "compile":
                        compileProjects.AddRange(projectPaths);
                        break;
                    default:
                        global::Serilog.Log.Warning($"Unknown action '{group.Action}' in group '{groupName}', treating as 'compile'");
                        compileProjects.AddRange(projectPaths);
                        break;
                }
            }
        }

        // Convert v2 ProjectGroups to v1-compatible BuildContext
        var context = new BuildContext
        {
            RepoRoot = repoRoot,
            HostsDir = hostsDir,
            PluginsDir = pluginsDir,
            ContractsDir = contractsDir,
            Solution = cfg.Solution is null ? null : repoRoot / cfg.Solution,
            NuGetOutputDir = nugetOutputDir,
            Version = version,
            ArtifactsVersion = artifactsVersion,
            IncludeHosts = includeHosts,
            ExcludeHosts = excludeHosts,
            IncludePlugins = includePlugins,
            ExcludePlugins = excludePlugins,
            IncludeContracts = includeContracts,
            ExcludeContracts = excludeContracts,
            CompileProjects = compileProjects.ToArray(),
            PublishProjects = publishProjects.ToArray(),
            PackProjects = packProjects.ToArray(),
            PackProperties = cfg.PackProperties ?? new Dictionary<string, string>()
        };

        return context;
    }

    private static (AbsolutePath hostsDir, AbsolutePath pluginsDir, AbsolutePath? contractsDir,
                    string[] includeHosts, string[] excludeHosts,
                    string[] includePlugins, string[] excludePlugins,
                    string[] includeContracts, string[] excludeContracts)
        MapProjectGroupsToV1Properties(BuildJsonConfig cfg, AbsolutePath repoRoot)
    {
        // For backward compatibility, map well-known group names to v1 properties
        // This allows existing UnifyBuildBase targets to work with v2 configs

        if (cfg.ProjectGroups is null)
        {
            // No groups - use defaults
            return (
                repoRoot / "project" / "hosts",
                repoRoot / "project" / "plugins",
                null,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()
            );
        }

        // Try to map common group names to v1 properties
        var hostsGroup = cfg.ProjectGroups.FirstOrDefault(kvp =>
            kvp.Key.Equals("executables", StringComparison.OrdinalIgnoreCase) ||
            kvp.Key.Equals("hosts", StringComparison.OrdinalIgnoreCase) ||
            kvp.Key.Equals("apps", StringComparison.OrdinalIgnoreCase)).Value;

        var pluginsGroup = cfg.ProjectGroups.FirstOrDefault(kvp =>
            kvp.Key.Equals("plugins", StringComparison.OrdinalIgnoreCase) ||
            kvp.Key.Equals("libraries", StringComparison.OrdinalIgnoreCase) ||
            kvp.Key.Equals("libs", StringComparison.OrdinalIgnoreCase)).Value;

        var contractsGroup = cfg.ProjectGroups.FirstOrDefault(kvp =>
            kvp.Key.Equals("contracts", StringComparison.OrdinalIgnoreCase) ||
            kvp.Key.Equals("packages", StringComparison.OrdinalIgnoreCase) ||
            kvp.Key.Equals("abstractions", StringComparison.OrdinalIgnoreCase)).Value;

        return (
            hostsGroup is not null ? repoRoot / hostsGroup.SourceDir : repoRoot / "project" / "hosts",
            pluginsGroup is not null ? repoRoot / pluginsGroup.SourceDir : repoRoot / "project" / "plugins",
            contractsGroup is not null ? repoRoot / contractsGroup.SourceDir : null,
            hostsGroup?.Include ?? Array.Empty<string>(),
            hostsGroup?.Exclude ?? Array.Empty<string>(),
            pluginsGroup?.Include ?? Array.Empty<string>(),
            pluginsGroup?.Exclude ?? Array.Empty<string>(),
            contractsGroup?.Include ?? Array.Empty<string>(),
            contractsGroup?.Exclude ?? Array.Empty<string>()
        );
    }

    private static List<string> DiscoverProjectsInGroup(AbsolutePath repoRoot, ProjectGroup group)
    {
        var sourceDir = repoRoot / group.SourceDir;
        if (!Directory.Exists(sourceDir))
        {
            global::Serilog.Log.Warning($"Source directory '{sourceDir}' does not exist, skipping group");
            return new List<string>();
        }

        var allProjects = Directory
            .GetFiles(sourceDir, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var projectNames = allProjects.Select(p => Path.GetFileNameWithoutExtension(p)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Apply include filter
        if (group.Include is not null && group.Include.Length > 0)
        {
            var includeSet = new HashSet<string>(group.Include, StringComparer.OrdinalIgnoreCase);
            allProjects = allProjects.Where(p => includeSet.Contains(Path.GetFileNameWithoutExtension(p))).ToList();
        }

        // Apply exclude filter
        if (group.Exclude is not null && group.Exclude.Length > 0)
        {
            var excludeSet = new HashSet<string>(group.Exclude, StringComparer.OrdinalIgnoreCase);
            allProjects = allProjects.Where(p => !excludeSet.Contains(Path.GetFileNameWithoutExtension(p))).ToList();
        }

        return allProjects;
    }

    private static string? GetEnv(string? name)
        => string.IsNullOrWhiteSpace(name) ? null : Environment.GetEnvironmentVariable(name);
}

