using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using static Nuke.Common.IO.FileSystemTasks;

namespace UnifyBuild.Nuke;

/// <summary>
/// Component for building native (CMake) projects.
/// Provides explicit 'Native' target for building C++ components.
/// </summary>
public interface IUnifyNative : IUnifyBuildConfig
{
    /// <summary>
    /// Build native components using CMake.
    /// </summary>
    Target Native => _ => _
        .Description("Build native (CMake) components")
        .Executes(() =>
        {
            var nativeConfig = UnifyConfig.NativeBuild;
            if (nativeConfig is null)
            {
                Serilog.Log.Information("No native build configuration found. Skipping.");
                return;
            }

            if (!nativeConfig.Enabled)
            {
                Serilog.Log.Information("Native build is disabled. Skipping.");
                return;
            }

            var cmakeListsPath = nativeConfig.CMakeSourceDir / "CMakeLists.txt";
            if (!File.Exists(cmakeListsPath))
            {
                Serilog.Log.Warning("CMakeLists.txt not found at {Path}. Skipping native build.", cmakeListsPath);
                return;
            }

            var vcpkgToolchain = TryDetectVcpkgToolchain(UnifyConfig.RepoRoot);
            var hasPreset = HasCMakePresets(nativeConfig.CMakeSourceDir, nativeConfig.CMakePreset);

            if (hasPreset && !string.IsNullOrEmpty(nativeConfig.CMakePreset))
            {
                BuildWithPreset(nativeConfig, vcpkgToolchain);
            }
            else
            {
                BuildWithConfigure(nativeConfig, vcpkgToolchain);
            }

            CollectArtifacts(nativeConfig);
        });

    private bool HasCMakePresets(AbsolutePath sourceDir, string? presetName)
    {
        var presetsPath = sourceDir / "CMakePresets.json";
        return File.Exists(presetsPath);
    }

    private string? TryDetectVcpkgToolchain(AbsolutePath repoRoot)
    {
        var toolchainPath = repoRoot / "vcpkg" / "scripts" / "buildsystems" / "vcpkg.cmake";
        return File.Exists(toolchainPath) ? toolchainPath : null;
    }

    private void BuildWithPreset(NativeBuildContext config, string? vcpkgToolchain)
    {
        Serilog.Log.Information("Building native with CMake preset: {Preset}", config.CMakePreset);

        // Configure with preset
        var configureArgs = $"--preset={config.CMakePreset}";
        if (!string.IsNullOrEmpty(vcpkgToolchain))
        {
            Serilog.Log.Information("Using vcpkg toolchain: {Path}", vcpkgToolchain);
            configureArgs += $" -DCMAKE_TOOLCHAIN_FILE=\"{vcpkgToolchain}\"";
        }

        ProcessTasks.StartProcess("cmake", configureArgs, config.CMakeSourceDir)
            .AssertZeroExitCode();

        // Build with preset
        var buildArgs = $"--build --preset={config.CMakePreset} --config {config.BuildConfig}";
        ProcessTasks.StartProcess("cmake", buildArgs, config.CMakeSourceDir)
            .AssertZeroExitCode();
    }

    private void BuildWithConfigure(NativeBuildContext config, string? vcpkgToolchain)
    {
        Serilog.Log.Information("Building native with CMake configure + build");

        EnsureExistingDirectory(config.CMakeBuildDir);

        // Configure
        var configureArgs = $"-S \"{config.CMakeSourceDir}\" -B \"{config.CMakeBuildDir}\" -DCMAKE_BUILD_TYPE={config.BuildConfig}";
        
        if (!string.IsNullOrEmpty(vcpkgToolchain))
        {
            Serilog.Log.Information("Using vcpkg toolchain: {Path}", vcpkgToolchain);
            configureArgs += $" -DCMAKE_TOOLCHAIN_FILE=\"{vcpkgToolchain}\"";
        }

        foreach (var option in config.CMakeOptions)
        {
            configureArgs += $" {option}";
        }

        ProcessTasks.StartProcess("cmake", configureArgs, config.CMakeSourceDir)
            .AssertZeroExitCode();

        // Build
        var buildArgs = $"--build \"{config.CMakeBuildDir}\" --config {config.BuildConfig}";
        ProcessTasks.StartProcess("cmake", buildArgs, config.CMakeSourceDir)
            .AssertZeroExitCode();
    }

    private void CollectArtifacts(NativeBuildContext config)
    {
        if (config.OutputDir is null)
        {
            Serilog.Log.Debug("No output directory configured for native artifacts");
            return;
        }

        EnsureExistingDirectory(config.OutputDir);

        var buildOutputDir = config.CMakeBuildDir / config.BuildConfig;
        if (!Directory.Exists(buildOutputDir))
        {
            buildOutputDir = config.CMakeBuildDir;
        }

        foreach (var pattern in config.ArtifactPatterns)
        {
            var files = Directory.GetFiles(buildOutputDir, pattern, SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                var destFile = config.OutputDir / Path.GetFileName(file);
                File.Copy(file, destFile, overwrite: true);
                Serilog.Log.Information("Collected native artifact: {File}", Path.GetFileName(file));
            }
        }
    }
}
