using System;
using Nuke.Common.IO;

namespace UnifyBuild.Nuke;

/// <summary>
/// Configuration for exporting Unity projects to platform builds (Windows, macOS, Android, iOS).
/// Separate from UnityBuildContext which handles DLL copying for Unity packages.
/// </summary>
public sealed record UnityExportContext
{
    /// <summary>
    /// Root directory of the Unity project containing Assets/.
    /// </summary>
    public AbsolutePath ProjectRoot { get; init; } = null!;

    /// <summary>
    /// Path to the Unity Editor executable.
    /// If null, resolved from UnityEditorPathEnv or common install locations.
    /// </summary>
    public string? EditorPath { get; init; }

    /// <summary>
    /// Environment variable name containing the Unity Editor path. Default: "UNITY_EDITOR_PATH".
    /// </summary>
    public string EditorPathEnv { get; init; } = "UNITY_EDITOR_PATH";

    /// <summary>
    /// The static method to invoke via -executeMethod.
    /// Default: "UnifyBuild.Editor.BuildScript.Build"
    /// </summary>
    public string ExecuteMethod { get; init; } = "UnifyBuild.Editor.BuildScript.Build";

    /// <summary>
    /// Platform export configurations.
    /// </summary>
    public UnityExportPlatformContext[] Platforms { get; init; } = Array.Empty<UnityExportPlatformContext>();

    /// <summary>
    /// Whether to use Fastlane for mobile distribution after Unity export.
    /// </summary>
    public bool UseFastlaneForMobile { get; init; } = false;

    /// <summary>
    /// Output root directory for exported builds.
    /// </summary>
    public AbsolutePath? OutputDir { get; init; }
}

/// <summary>
/// Platform-specific Unity export configuration.
/// </summary>
public sealed record UnityExportPlatformContext
{
    /// <summary>
    /// Unity BuildTarget name (e.g., "StandaloneWindows64", "Android", "iOS", "StandaloneOSX").
    /// </summary>
    public string BuildTarget { get; init; } = "";

    /// <summary>
    /// Output file or directory name for this platform export.
    /// </summary>
    public string OutputName { get; init; } = "";

    /// <summary>
    /// Additional arguments to pass to the Unity build method via environment variables.
    /// </summary>
    public Dictionary<string, string> BuildArgs { get; init; } = new();

    /// <summary>
    /// Whether this is a mobile platform.
    /// </summary>
    public bool IsMobile => BuildTarget.Equals("Android", StringComparison.OrdinalIgnoreCase)
                         || BuildTarget.Equals("iOS", StringComparison.OrdinalIgnoreCase);
}
