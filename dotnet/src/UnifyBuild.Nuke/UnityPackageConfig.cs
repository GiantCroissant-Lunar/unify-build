using Nuke.Common.IO;

namespace UnifyBuild.Nuke;

/// <summary>
/// Configuration for building and copying netstandard2.1 DLLs into Unity package directories.
/// </summary>
public sealed record UnityBuildContext
{
    /// <summary>
    /// Target framework for Unity builds. Default: "netstandard2.1"
    /// </summary>
    public string TargetFramework { get; init; } = "netstandard2.1";

    /// <summary>
    /// Root directory of the Unity project (e.g., "../mung-bean-app-unity").
    /// Resolved relative to RepoRoot.
    /// </summary>
    public AbsolutePath UnityProjectRoot { get; init; } = null!;

    /// <summary>
    /// Package mappings defining which projects map to which Unity packages.
    /// </summary>
    public UnityPackageMapping[] Packages { get; init; } = [];
}

/// <summary>
/// Maps one or more .NET projects to a Unity package directory.
/// </summary>
public sealed record UnityPackageMapping
{
    /// <summary>
    /// Unity package name (e.g., "com.giantcroissant.fantasim.contracts").
    /// </summary>
    public string PackageName { get; init; } = "";

    /// <summary>
    /// Scoped index directory (e.g., "scoped-3208").
    /// </summary>
    public string ScopedIndex { get; init; } = "";

    /// <summary>
    /// Source project paths to build (relative to RepoRoot).
    /// If empty, only DependencyDlls are copied (no build step).
    /// </summary>
    public string[] SourceProjects { get; init; } = [];

    /// <summary>
    /// Glob patterns for source projects (e.g., "contracts/*").
    /// Expanded at load time to discover all matching .csproj files.
    /// </summary>
    public string[] SourceProjectGlobs { get; init; } = [];

    /// <summary>
    /// Names of transitive dependency DLLs to copy from build output
    /// into the Unity package Runtime directory. These are resolved from
    /// the build output of source projects or from NuGet package caches.
    /// </summary>
    public string[] DependencyDlls { get; init; } = [];
}
