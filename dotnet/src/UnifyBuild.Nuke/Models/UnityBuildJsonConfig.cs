namespace UnifyBuild.Nuke;

/// <summary>
/// JSON configuration for Unity package builds.
/// </summary>
public sealed class UnityBuildJsonConfig
{
    /// <summary>
    /// Target framework to build. Default: "netstandard2.1"
    /// </summary>
    public string? TargetFramework { get; set; }

    /// <summary>
    /// Root directory of the Unity project, relative to repo root.
    /// </summary>
    public string UnityProjectRoot { get; set; } = "";

    /// <summary>
    /// Package mappings from .NET projects to Unity packages.
    /// </summary>
    public UnityPackageMappingConfig[]? Packages { get; set; }
}

/// <summary>
/// JSON configuration for a single Unity package mapping.
/// </summary>
public sealed class UnityPackageMappingConfig
{
    /// <summary>
    /// Unity package name (e.g., "com.giantcroissant.fantasim.contracts").
    /// </summary>
    public string PackageName { get; set; } = "";

    /// <summary>
    /// Scoped index directory (e.g., "scoped-3208").
    /// </summary>
    public string ScopedIndex { get; set; } = "";

    /// <summary>
    /// Explicit source project paths to build (relative to repo root, e.g., "project/contracts/Foo/Foo.csproj").
    /// </summary>
    public string[]? SourceProjects { get; set; }

    /// <summary>
    /// Glob patterns for discovering source projects (e.g., "project/contracts/*").
    /// Each pattern is expanded to find .csproj files in matching directories.
    /// </summary>
    public string[]? SourceProjectGlobs { get; set; }

    /// <summary>
    /// Transitive dependency DLL names to copy from build output.
    /// </summary>
    public string[]? DependencyDlls { get; set; }
}
