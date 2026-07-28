namespace UnifyBuild.Nuke;

/// <summary>
/// JSON configuration for Godot game exports.
/// </summary>
public sealed class GodotBuildConfig
{
    /// <summary>
    /// Root directory of the Godot project containing project.godot.
    /// </summary>
    public string ProjectRoot { get; set; } = "";

    /// <summary>
    /// Name of the environment variable containing the path to the Godot executable.
    /// Defaults to "GODOT".
    /// </summary>
    public string? ExecutablePathEnv { get; set; } = "GODOT";

    /// <summary>
    /// Explicit path to the Godot executable, overrides ExecutablePathEnv.
    /// </summary>
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// The name of the central game assembly.
    /// </summary>
    public string? AssemblyName { get; set; }

    /// <summary>
    /// Platforms to export.
    /// </summary>
    public GodotExportPlatformConfig[]? Platforms { get; set; }

    /// <summary>
    /// Android keystore path for signing APK/AAB exports.
    /// Can also be set via GODOT_ANDROID_KEYSTORE_PATH env var.
    /// </summary>
    public string? AndroidKeystorePath { get; set; }

    /// <summary>
    /// Whether to use Fastlane for mobile distribution after Godot export.
    /// </summary>
    public bool UseFastlaneForMobile { get; set; } = false;
}

/// <summary>
/// Platform configuration for Godot export.
/// </summary>
public sealed class GodotExportPlatformConfig
{
    /// <summary>
    /// The .NET Runtime Identifier (RID) for the platform (e.g., win-x64, linux-x64, osx).
    /// Used for the output directory name and version metadata only; Godot's own
    /// C# export plugin handles per-arch `dotnet publish` during --export-release.
    /// </summary>
    public string Rid { get; set; } = "";

    /// <summary>
    /// The Godot export preset name (e.g., "Windows Desktop", "Linux", "macOS").
    /// </summary>
    public string PresetName { get; set; } = "";

    /// <summary>
    /// The name of the binary file to export (e.g., "complete-app.zip").
    /// </summary>
    public string BinaryName { get; set; } = "";
}
