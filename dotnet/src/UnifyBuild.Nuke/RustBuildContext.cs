using Nuke.Common.IO;

namespace UnifyBuild.Nuke;

/// <summary>
/// Configuration for Rust (Cargo) builds.
/// </summary>
public sealed record RustBuildContext
{
    /// <summary>
    /// Whether Rust builds are enabled. Default: true.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Directory containing Cargo.toml.
    /// </summary>
    public AbsolutePath? CargoManifestDir { get; init; }

    /// <summary>
    /// Cargo build profile (debug, release, or custom). Default: "release".
    /// </summary>
    public string Profile { get; init; } = "release";

    /// <summary>
    /// Cargo features to enable.
    /// </summary>
    public string[] Features { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Target triple for cross-compilation (e.g., "x86_64-pc-windows-msvc").
    /// </summary>
    public string? TargetTriple { get; init; }

    /// <summary>
    /// Output directory for Rust artifacts.
    /// </summary>
    public AbsolutePath? OutputDir { get; init; }

    /// <summary>
    /// File patterns to collect as artifacts (e.g., "*.dll", "*.so", "*.exe").
    /// </summary>
    public string[] ArtifactPatterns { get; init; } = new[] { "*.dll", "*.so", "*.dylib", "*.exe" };
}
