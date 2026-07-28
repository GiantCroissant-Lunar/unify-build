namespace UnifyBuild.Nuke;

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
