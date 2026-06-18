using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using System.Text.Json;

namespace UnifyBuild.Nuke;

public interface IUnifyGodot : IUnifyCompile
{
    /// <summary>
    /// Build and export Godot project for all configured platforms.
    /// Desktop platforms produce final binaries.
    /// Mobile platforms export native projects (Gradle/Xcode) for Fastlane to build.
    /// </summary>
    Target BuildGodot => _ => _
        .DependsOn<IUnifyCompile>(x => x.Compile)
        .Executes(() =>
        {
            var config = UnifyConfig.GodotBuild;
            if (config is null)
                return;

            var godotPath = ResolveGodotPath(config);
            var projectRoot = config.ProjectRoot;
            var csproj = Directory.GetFiles(projectRoot, "*.csproj").FirstOrDefault()
                ?? throw new InvalidOperationException($"No .csproj found in Godot project root: {projectRoot}");

            var assemblyName = config.AssemblyName ?? Path.GetFileNameWithoutExtension(csproj);
            var semver = UnifyConfig.Version ?? "0.1.0";
            var artifactsDir = config.OutputDir;

            // Split platforms into desktop and mobile
            var desktopPlatforms = config.Platforms.Where(p => !p.IsMobile).ToArray();
            var mobilePlatforms = config.Platforms.Where(p => p.IsMobile).ToArray();

            // Desktop: full export (existing behavior)
            foreach (var platform in desktopPlatforms)
            {
                ExportDesktopPlatform(godotPath, config, csproj, assemblyName, semver, artifactsDir, platform);
            }

            // Mobile: export native project for Fastlane
            foreach (var platform in mobilePlatforms)
            {
                ExportMobileProject(godotPath, config, csproj, artifactsDir, platform);
            }
        });

    /// <summary>
    /// Export Godot project for desktop platforms only.
    /// </summary>
    Target BuildGodotDesktop => _ => _
        .DependsOn<IUnifyCompile>(x => x.Compile)
        .Description("Export Godot project for desktop platforms only")
        .Executes(() =>
        {
            var config = UnifyConfig.GodotBuild;
            if (config is null) return;

            var godotPath = ResolveGodotPath(config);
            var csproj = Directory.GetFiles(config.ProjectRoot, "*.csproj").FirstOrDefault()
                ?? throw new InvalidOperationException("No .csproj found in Godot project root.");

            var assemblyName = config.AssemblyName ?? Path.GetFileNameWithoutExtension(csproj);
            var semver = UnifyConfig.Version ?? "0.1.0";
            var artifactsDir = config.OutputDir;

            foreach (var platform in config.Platforms.Where(p => !p.IsMobile))
            {
                ExportDesktopPlatform(godotPath, config, csproj, assemblyName, semver, artifactsDir, platform);
            }
        });

    /// <summary>
    /// Export Godot project as native projects for mobile platforms (Gradle/Xcode).
    /// The exported projects are then built by Fastlane via IUnifyMobile.
    /// </summary>
    Target BuildGodotMobile => _ => _
        .DependsOn<IUnifyCompile>(x => x.Compile)
        .Description("Export Godot mobile platforms as Gradle/Xcode projects for Fastlane")
        .Executes(() =>
        {
            var config = UnifyConfig.GodotBuild;
            if (config is null) return;

            var godotPath = ResolveGodotPath(config);
            var csproj = Directory.GetFiles(config.ProjectRoot, "*.csproj").FirstOrDefault()
                ?? throw new InvalidOperationException("No .csproj found in Godot project root.");

            var artifactsDir = config.OutputDir;

            foreach (var platform in config.Platforms.Where(p => p.IsMobile))
            {
                ExportMobileProject(godotPath, config, csproj, artifactsDir, platform);
            }
        });

    // --- helpers ---

    sealed string ResolveGodotPath(GodotBuildContext config)
    {
        var godotPath = config.ExecutablePath;
        if (string.IsNullOrEmpty(godotPath) && !string.IsNullOrEmpty(config.ExecutablePathEnv))
            godotPath = Environment.GetEnvironmentVariable(config.ExecutablePathEnv);

        if (string.IsNullOrEmpty(godotPath) || !File.Exists(godotPath))
            throw new InvalidOperationException($"Godot executable not found at '{godotPath}'. Verify GODOT env var.");

        return godotPath;
    }

    sealed void ExportDesktopPlatform(
        string godotPath, GodotBuildContext config, string csproj,
        string assemblyName, string semver, AbsolutePath artifactsDir,
        GodotExportPlatformContext platform)
    {
        var rid = platform.Rid;
        var outDir = artifactsDir / rid;
        outDir.CreateOrCleanDirectory();

        // Godot's C# export plugin (GodotTools.Export.ExportPlugin) already runs
        // `dotnet publish` per target architecture during --export-release and
        // writes the output into per-arch data_* directories inside the bundle
        // (e.g. data_<assembly>_macos_arm64, data_<assembly>_macos_x86_64).
        // Doing a separate dotnet publish + DLL injection here was redundant and
        // wrong: it only targeted a single data dir and overwrote Godot's
        // correctly-published per-arch DLLs with a single-arch publish output.

        var exportPath = outDir / platform.BinaryName;
        global::Serilog.Log.Information($"Exporting [{rid}] -> {platform.PresetName}...");

        try
        {
            ProcessTasks.StartProcess(
                godotPath,
                $"--headless --path {config.ProjectRoot} --export-release {platform.PresetName} {exportPath}",
                workingDirectory: config.ProjectRoot
            ).AssertZeroExitCode();
        }
        catch (Exception ex)
        {
            if (!File.Exists(exportPath))
                throw new InvalidOperationException($"Export failed for {rid} — no binary at {exportPath}", ex);
        }

        // macOS exports produce a .zip. Extract it so the .app bundle is
        // directly runnable without a manual unzip step. The .zip is kept
        // for distribution/CI use cases that expect a single-file artifact.
        if (exportPath.Extension == ".zip" && File.Exists(exportPath))
        {
            ZipFile.ExtractToDirectory(exportPath, outDir, overwriteFiles: true);
            global::Serilog.Log.Information($"Extracted runnable bundle -> {outDir}");
        }

        WriteVersionJson(outDir, semver, rid, platform.PresetName);
        global::Serilog.Log.Information($"Done: {outDir}");
    }

    sealed void ExportMobileProject(
        string godotPath, GodotBuildContext config, string csproj,
        AbsolutePath artifactsDir, GodotExportPlatformContext platform)
    {
        var rid = platform.Rid;
        var outDir = artifactsDir / $"{rid}-project";
        outDir.CreateOrCleanDirectory();

        global::Serilog.Log.Information("Exporting [{Rid}] as native project -> {PresetName}...", rid, platform.PresetName);

        // For Android: Godot uses --export-pack or the "Export Android Project" preset option
        // For iOS: Godot always exports an Xcode project
        // Use --export-debug to get the project structure without final packaging
        var exportFlag = rid.StartsWith("android", StringComparison.OrdinalIgnoreCase)
            ? "--export-debug"   // Android: export preset must have "export_as_project" enabled
            : "--export-release"; // iOS: always produces Xcode project

        var exportPath = outDir / platform.BinaryName;

        try
        {
            ProcessTasks.StartProcess(
                godotPath,
                $"--headless --path {config.ProjectRoot} {exportFlag} {platform.PresetName} {exportPath}",
                workingDirectory: config.ProjectRoot
            ).AssertZeroExitCode();
        }
        catch (Exception ex)
        {
            if (!Directory.Exists(outDir) || !Directory.EnumerateFileSystemEntries(outDir).Any())
                throw new InvalidOperationException($"Mobile project export failed for {rid}", ex);
        }

        // Set EXPORTED_PROJECT_DIR so Fastlane knows where to find the project
        Environment.SetEnvironmentVariable("EXPORTED_PROJECT_DIR", outDir);

        global::Serilog.Log.Information("Exported {Rid} native project to {OutDir}. " +
            "Use MobileBuildAndroidFromProject or MobileBuildIosFromProject to build with Fastlane.", rid, outDir);
    }

    sealed void WriteVersionJson(AbsolutePath outDir, string semver, string rid, string presetName)
    {
        var versionData = new
        {
            SemVer = semver,
            Platform = rid,
            Preset = presetName,
            ExportedAt = DateTime.UtcNow.ToString("o")
        };
        File.WriteAllText(outDir / "version.json",
            JsonSerializer.Serialize(versionData, new JsonSerializerOptions { WriteIndented = true }));
    }
}
