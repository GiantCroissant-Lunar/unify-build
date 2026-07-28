namespace UnifyBuild.Nuke;

/// <summary>
/// JSON configuration for Unity platform exports (standalone/mobile builds).
/// </summary>
public sealed class UnityExportConfig
{
    /// <summary>
    /// Root directory of the Unity project containing Assets/.
    /// </summary>
    public string ProjectRoot { get; set; } = "";

    /// <summary>
    /// Path to the Unity Editor executable.
    /// </summary>
    public string? EditorPath { get; set; }

    /// <summary>
    /// Environment variable name containing the Unity Editor path. Default: "UNITY_EDITOR_PATH".
    /// </summary>
    public string? EditorPathEnv { get; set; }

    /// <summary>
    /// The static method to invoke via -executeMethod.
    /// Default: "UnifyBuild.Editor.BuildScript.Build"
    /// </summary>
    public string? ExecuteMethod { get; set; }

    /// <summary>
    /// Platform export configurations.
    /// </summary>
    public UnityExportPlatformConfig[]? Platforms { get; set; }

    /// <summary>
    /// Whether to use Fastlane for mobile distribution after Unity export.
    /// </summary>
    public bool UseFastlaneForMobile { get; set; } = false;

    /// <summary>
    /// Output root directory for exported builds.
    /// </summary>
    public string? OutputDir { get; set; }
}

/// <summary>
/// Platform-specific Unity export configuration for JSON deserialization.
/// </summary>
public sealed class UnityExportPlatformConfig
{
    /// <summary>
    /// Unity BuildTarget name (e.g., "StandaloneWindows64", "Android", "iOS", "StandaloneOSX").
    /// </summary>
    public string BuildTarget { get; set; } = "";

    /// <summary>
    /// Output file or directory name for this platform export.
    /// </summary>
    public string? OutputName { get; set; }

    /// <summary>
    /// Additional arguments to pass to the Unity build method via environment variables.
    /// </summary>
    public Dictionary<string, string>? BuildArgs { get; set; }
}
