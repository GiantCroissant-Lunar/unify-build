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
}

public sealed record GodotExportPlatformContext
{
    public string Rid { get; init; } = "";
    public string PresetName { get; init; } = "";
    public string BinaryName { get; init; } = "";
    public string DataDirName { get; init; } = "";
}
