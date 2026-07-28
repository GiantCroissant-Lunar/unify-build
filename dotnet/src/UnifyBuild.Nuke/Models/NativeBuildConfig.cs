using System.Text.Json.Serialization;

namespace UnifyBuild.Nuke;

/// <summary>
/// Native (CMake) build configuration.
/// </summary>
public sealed class NativeBuildConfig
{
    /// <summary>
    /// Whether native builds are enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Source directory containing CMakeLists.txt. Default: "native"
    /// </summary>
    [JsonPropertyName("cmakeSourceDir")]
    public string? CMakeSourceDir { get; set; }

    /// <summary>
    /// Build directory for CMake. Default: "native/build"
    /// </summary>
    [JsonPropertyName("cmakeBuildDir")]
    public string? CMakeBuildDir { get; set; }

    /// <summary>
    /// CMake preset name to use (requires CMakePresets.json).
    /// </summary>
    [JsonPropertyName("cmakePreset")]
    public string? CMakePreset { get; set; }

    /// <summary>
    /// Additional CMake configuration options.
    /// </summary>
    [JsonPropertyName("cmakeOptions")]
    public string[]? CMakeOptions { get; set; }

    /// <summary>
    /// Build configuration (Release, Debug, etc.). Default: "Release"
    /// </summary>
    public string? BuildConfig { get; set; }

    /// <summary>
    /// Auto-detect and use vcpkg toolchain if present. Default: true
    /// </summary>
    public bool AutoDetectVcpkg { get; set; } = true;

    /// <summary>
    /// Output directory for native artifacts. Default: "build/_artifacts/{version}/native"
    /// </summary>
    public string? OutputDir { get; set; }

    /// <summary>
    /// File patterns to collect as artifacts.
    /// </summary>
    public string[]? ArtifactPatterns { get; set; }

    /// <summary>
    /// Custom commands to execute before and/or after the CMake build.
    /// Each entry is a shell command string.
    /// </summary>
    public string[]? CustomCommands { get; set; }

    /// <summary>
    /// Target platform for platform-specific configuration (e.g., "windows", "linux", "macos").
    /// If null, the current OS platform is used.
    /// </summary>
    public string? Platform { get; set; }
}
