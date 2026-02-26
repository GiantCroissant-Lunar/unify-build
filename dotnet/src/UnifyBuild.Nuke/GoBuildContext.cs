using Nuke.Common.IO;

namespace UnifyBuild.Nuke;

/// <summary>
/// Configuration for Go builds.
/// </summary>
public sealed record GoBuildContext
{
    /// <summary>
    /// Whether Go builds are enabled. Default: true.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Directory containing go.mod.
    /// </summary>
    public AbsolutePath? GoModuleDir { get; init; }

    /// <summary>
    /// Build flags to pass to go build (e.g., ["-ldflags", "-s -w"]).
    /// </summary>
    public string[] BuildFlags { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Output binary name (used with -o flag).
    /// </summary>
    public string? OutputBinary { get; init; }

    /// <summary>
    /// Output directory for Go artifacts.
    /// </summary>
    public AbsolutePath? OutputDir { get; init; }

    /// <summary>
    /// Environment variables for the go build process (e.g., GOOS, GOARCH).
    /// </summary>
    public Dictionary<string, string> EnvVars { get; init; } = new();
}
