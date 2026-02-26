using Nuke.Common.IO;

namespace UnifyBuild.Nuke;

/// <summary>
/// Configuration for native (CMake) builds.
/// </summary>
public sealed record NativeBuildContext
{
    /// <summary>
    /// Whether native builds are enabled. Default: true if CMakeLists.txt exists.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Source directory containing CMakeLists.txt. Default: "native"
    /// </summary>
    public AbsolutePath? CMakeSourceDir { get; init; }

    /// <summary>
    /// Build directory for CMake. Default: "native/build"
    /// </summary>
    public AbsolutePath? CMakeBuildDir { get; init; }

    /// <summary>
    /// CMake preset name to use (requires CMakePresets.json).
    /// </summary>
    public string? CMakePreset { get; init; }

    /// <summary>
    /// Additional CMake configuration options.
    /// </summary>
    public string[] CMakeOptions { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Build configuration (Release, Debug, etc.). Default: "Release"
    /// </summary>
    public string BuildConfig { get; init; } = "Release";

    /// <summary>
    /// Auto-detect and use vcpkg toolchain if present. Default: true
    /// </summary>
    public bool AutoDetectVcpkg { get; init; } = true;

    /// <summary>
    /// Output directory for native artifacts. Default: "build/_artifacts/{version}/native"
    /// </summary>
    public AbsolutePath? OutputDir { get; init; }

    /// <summary>
    /// File patterns to collect as artifacts (e.g., "*.dll", "*.so", "*.dylib").
    /// </summary>
    public string[] ArtifactPatterns { get; init; } = new[] { "*.dll", "*.so", "*.dylib", "*.lib", "*.a" };

    /// <summary>
    /// Custom commands to execute before and/or after the CMake build.
    /// </summary>
    public string[] CustomCommands { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Target platform for platform-specific configuration (e.g., "windows", "linux", "macos").
    /// If null, the current OS platform is used.
    /// </summary>
    public string? Platform { get; init; }
}
