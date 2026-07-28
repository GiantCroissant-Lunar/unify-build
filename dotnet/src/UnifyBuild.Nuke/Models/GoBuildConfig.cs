namespace UnifyBuild.Nuke;

/// <summary>
/// JSON configuration for Go builds.
/// </summary>
public sealed class GoBuildConfig
{
    /// <summary>
    /// Whether Go builds are enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Directory containing go.mod. Relative to repo root.
    /// </summary>
    public string? GoModuleDir { get; set; }

    /// <summary>
    /// Build flags to pass to go build (e.g., ["-ldflags", "-s -w"]).
    /// </summary>
    public string[]? BuildFlags { get; set; }

    /// <summary>
    /// Output binary name (used with -o flag).
    /// </summary>
    public string? OutputBinary { get; set; }

    /// <summary>
    /// Output directory for Go artifacts.
    /// </summary>
    public string? OutputDir { get; set; }

    /// <summary>
    /// Environment variables for the go build process (e.g., GOOS, GOARCH).
    /// </summary>
    public Dictionary<string, string>? EnvVars { get; set; }
}
