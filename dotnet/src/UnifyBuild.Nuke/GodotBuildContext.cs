using System;
using Nuke.Common.IO;

namespace UnifyBuild.Nuke;

public sealed record GodotBuildContext
{
    public AbsolutePath ProjectRoot { get; init; } = null!;
    public string? ExecutablePathEnv { get; init; }
    public string? ExecutablePath { get; init; }
    public string? AssemblyName { get; init; }
    public GodotExportPlatformContext[] Platforms { get; init; } = Array.Empty<GodotExportPlatformContext>();

    /// <summary>
    /// Android keystore path for signing APK/AAB exports.
    /// Can also be set via GODOT_ANDROID_KEYSTORE_PATH env var.
    /// </summary>
    public string? AndroidKeystorePath { get; init; }

    /// <summary>
    /// Whether to use Fastlane for mobile distribution after export.
    /// When true, the MobileBuild config is used for deployment.
    /// </summary>
    public bool UseFastlaneForMobile { get; init; } = false;

    /// <summary>
    /// Output directory for Godot export artifacts.
    /// Defaults to build/_artifacts/{version}/godot.
    /// </summary>
    public AbsolutePath OutputDir { get; init; } = null!;
}

public sealed record GodotExportPlatformContext
{
    public string Rid { get; init; } = "";
    public string PresetName { get; init; } = "";
    public string BinaryName { get; init; } = "";
    public string DataDirName { get; init; } = "";

    /// <summary>
    /// Whether this is a mobile platform (android, ios).
    /// Determined from the Rid prefix.
    /// </summary>
    public bool IsMobile => Rid.StartsWith("android", StringComparison.OrdinalIgnoreCase)
                         || Rid.StartsWith("ios", StringComparison.OrdinalIgnoreCase);
}
