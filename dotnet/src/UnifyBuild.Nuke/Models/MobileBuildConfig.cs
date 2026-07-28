namespace UnifyBuild.Nuke;

/// <summary>
/// JSON configuration for mobile (iOS/Android) builds using Fastlane.
/// </summary>
public sealed class MobileBuildConfig
{
    /// <summary>
    /// Whether mobile builds are enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Root directory containing mobile platform subdirectories (ios/, android/).
    /// Default: "mobile"
    /// </summary>
    public string? MobileRoot { get; set; }

    /// <summary>
    /// iOS-specific configuration.
    /// </summary>
    public MobilePlatformConfig? Ios { get; set; }

    /// <summary>
    /// Android-specific configuration.
    /// </summary>
    public MobilePlatformConfig? Android { get; set; }

    /// <summary>
    /// Output directory for mobile build artifacts.
    /// </summary>
    public string? OutputDir { get; set; }
}

/// <summary>
/// Platform-specific mobile build configuration for JSON deserialization.
/// </summary>
public sealed class MobilePlatformConfig
{
    /// <summary>
    /// Whether this platform is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Additional environment variables to pass to Fastlane.
    /// </summary>
    public Dictionary<string, string>? EnvVars { get; set; }

    /// <summary>
    /// Custom Fastlane lane name for build. Default: "build".
    /// </summary>
    public string? BuildLane { get; set; }

    /// <summary>
    /// Custom Fastlane lane name for beta deployment. Default: "beta".
    /// </summary>
    public string? BetaLane { get; set; }

    /// <summary>
    /// Custom Fastlane lane name for release deployment. Default: "release".
    /// </summary>
    public string? ReleaseLane { get; set; }
}
