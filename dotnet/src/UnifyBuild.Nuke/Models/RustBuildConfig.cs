namespace UnifyBuild.Nuke;

/// <summary>
/// JSON configuration for Rust (Cargo) builds.
/// </summary>
public sealed class RustBuildConfig
{
    /// <summary>
    /// Whether Rust builds are enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Directory containing Cargo.toml. Relative to repo root.
    /// </summary>
    public string? CargoManifestDir { get; set; }

    /// <summary>
    /// Cargo build profile (debug, release, or custom). Default: "release".
    /// </summary>
    public string Profile { get; set; } = "release";

    /// <summary>
    /// Cargo features to enable.
    /// </summary>
    public string[]? Features { get; set; }

    /// <summary>
    /// Target triple for cross-compilation (e.g., "x86_64-pc-windows-msvc").
    /// </summary>
    public string? TargetTriple { get; set; }

    /// <summary>
    /// Output directory for Rust artifacts. Default: "build/_artifacts/{version}/rust".
    /// </summary>
    public string? OutputDir { get; set; }

    /// <summary>
    /// File patterns to collect as artifacts (e.g., "*.dll", "*.so", "*.exe").
    /// </summary>
    public string[]? ArtifactPatterns { get; set; }
}
