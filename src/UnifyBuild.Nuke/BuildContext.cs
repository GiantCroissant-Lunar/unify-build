using System;
using Nuke.Common.IO;

namespace UnifyBuild.Nuke;

public sealed record BuildContext
{
    public AbsolutePath RepoRoot { get; init; } = null!;
    public AbsolutePath HostsDir { get; init; } = null!;
    public AbsolutePath PluginsDir { get; init; } = null!;
    public AbsolutePath? Solution { get; init; }

    /// <summary>
    /// Directory containing contract/abstraction projects to be packed as NuGet packages.
    /// </summary>
    public AbsolutePath? ContractsDir { get; init; }

    /// <summary>
    /// Output directory for NuGet packages. Defaults to build/_artifacts/{version}/nuget.
    /// </summary>
    public AbsolutePath? NuGetOutputDir { get; init; }

    public string[] IncludeHosts { get; init; } = Array.Empty<string>();
    public string[] ExcludeHosts { get; init; } = Array.Empty<string>();
    public string[] IncludePlugins { get; init; } = Array.Empty<string>();
    public string[] ExcludePlugins { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Explicit list of contract project names to include for packing.
    /// If empty, all projects in ContractsDir are included.
    /// </summary>
    public string[] IncludeContracts { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Contract project names to exclude from packing.
    /// </summary>
    public string[] ExcludeContracts { get; init; } = Array.Empty<string>();

    public string[] CompileProjects { get; init; } = Array.Empty<string>();

    public string[] PublishProjects { get; init; } = Array.Empty<string>();

    public string[] PackProjects { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Optional MSBuild Version value to forward as /p:Version=... when publishing.
    /// If null, no explicit Version property is passed.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Optional MSBuild ArtifactsVersion value to forward as /p:ArtifactsVersion=... when publishing.
    /// If null, ArtifactsVersion will fall back to Version or ultimately to "local".
    /// </summary>
    public string? ArtifactsVersion { get; init; }

    /// <summary>
    /// Additional MSBuild properties to pass during pack operations.
    /// Key-value pairs like "UseDevelopmentReferences=false".
    /// </summary>
    public Dictionary<string, string> PackProperties { get; init; } = new();
}
