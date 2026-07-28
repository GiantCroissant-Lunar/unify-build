using System.Text.Json.Serialization;

namespace UnifyBuild.Nuke;

/// <summary>
/// Generic build configuration schema using flexible project groups.
/// Replaces domain-specific terminology (HostsDir, PluginsDir, ContractsDir)
/// with architecture-agnostic project groups organized by build action.
/// </summary>
public sealed class BuildJsonConfig
{
    /// <summary>
    /// Optional JSON Schema reference used by editors and validators.
    /// </summary>
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

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

    public bool PackIncludeSymbols { get; set; } = false;

    // Local NuGet feed sync settings (carried over from v1)
    public bool SyncLocalNugetFeed { get; set; } = false;
    public string? LocalNugetFeedRoot { get; set; }
    public string? LocalNugetFeedFlatSubdir { get; set; } = "flat";
    public string? LocalNugetFeedHierarchicalSubdir { get; set; } = "hierarchical";
    public string? LocalNugetFeedBaseUrl { get; set; }

    /// <summary>
    /// Native (CMake) build configuration.
    /// </summary>
    public NativeBuildConfig? NativeBuild { get; set; }

    /// <summary>
    /// Rust (Cargo) build configuration.
    /// </summary>
    public RustBuildConfig? RustBuild { get; set; }

    /// <summary>
    /// Go build configuration.
    /// </summary>
    public GoBuildConfig? GoBuild { get; set; }

    /// <summary>
    /// Unity package build configuration for copying netstandard2.1 DLLs to Unity packages.
    /// </summary>
    public UnityBuildJsonConfig? UnityBuild { get; set; }

    /// <summary>
    /// Godot build configuration for exporting games.
    /// </summary>
    public GodotBuildConfig? GodotBuild { get; set; }

    /// <summary>
    /// Mobile (iOS/Android) build configuration using Fastlane.
    /// </summary>
    public MobileBuildConfig? MobileBuild { get; set; }

    /// <summary>
    /// Unity platform export configuration for building standalone/mobile apps.
    /// Separate from UnityBuild which handles DLL copying for Unity packages.
    /// </summary>
    public UnityExportConfig? UnityExport { get; set; }

    /// <summary>
    /// Advanced package management configuration (multi-registry push, signing, SBOM, retention).
    /// </summary>
    public PackageManagementConfig? PackageManagement { get; set; }

    /// <summary>
    /// Performance configuration (caching, change detection).
    /// </summary>
    public PerformanceConfig? Performance { get; set; }

    /// <summary>
    /// Observability configuration (metrics, telemetry).
    /// </summary>
    public ObservabilityConfig? Observability { get; set; }

    /// <summary>
    /// Extensions configuration for loading custom components from external assemblies.
    /// </summary>
    public ExtensionsConfig? Extensions { get; set; }
}
