using Nuke.Common.IO;

namespace UnifyBuild.Nuke;

/// <summary>
/// Configuration for mobile (iOS/Android) builds using Fastlane.
/// </summary>
public sealed record MobileBuildContext
{
    /// <summary>
    /// Whether mobile builds are enabled. Default: true.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Root directory containing mobile platform subdirectories (ios/, android/).
    /// Default: "mobile"
    /// </summary>
    public AbsolutePath MobileRoot { get; init; } = null!;

    /// <summary>
    /// iOS-specific configuration. Null if iOS builds are not configured.
    /// </summary>
    public MobilePlatformContext? Ios { get; init; }

    /// <summary>
    /// Android-specific configuration. Null if Android builds are not configured.
    /// </summary>
    public MobilePlatformContext? Android { get; init; }

    /// <summary>
    /// Output directory for mobile build artifacts (e.g., .ipa, .apk, .aab).
    /// </summary>
    public AbsolutePath? OutputDir { get; init; }
}

/// <summary>
/// Platform-specific mobile build configuration.
/// </summary>
public sealed record MobilePlatformContext
{
    /// <summary>
    /// Whether this platform is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Working directory for Fastlane (contains the fastlane/ subdirectory).
    /// </summary>
    public AbsolutePath WorkingDir { get; init; } = null!;

    /// <summary>
    /// Additional environment variables to pass to Fastlane.
    /// </summary>
    public Dictionary<string, string> EnvVars { get; init; } = new();

    /// <summary>
    /// Custom Fastlane lane name for standalone build. Default: "build".
    /// </summary>
    public string BuildLane { get; init; } = "build";

    /// <summary>
    /// Fastlane lane name for building from an engine-exported project (Gradle/Xcode).
    /// Default: "build_from_project".
    /// </summary>
    public string BuildFromProjectLane { get; init; } = "build_from_project";

    /// <summary>
    /// Custom Fastlane lane name for beta deployment. Default: "beta".
    /// </summary>
    public string BetaLane { get; init; } = "beta";

    /// <summary>
    /// Custom Fastlane lane name for release deployment. Default: "release".
    /// </summary>
    public string ReleaseLane { get; init; } = "release";
}
