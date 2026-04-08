using System.Runtime.InteropServices;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;

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

            if (!IsPlatformMatch(nativeConfig.Platform))
            {
                Serilog.Log.Information(
                    "Native build platform '{Platform}' does not match current OS. Skipping.",
                    nativeConfig.Platform);
                return;
            }

            var sourceDir = nativeConfig.CMakeSourceDir
                ?? throw new InvalidOperationException("Native build context is missing CMakeSourceDir.");
            var buildDir = nativeConfig.CMakeBuildDir
                ?? throw new InvalidOperationException("Native build context is missing CMakeBuildDir.");

            var cmakeListsPath = sourceDir / "CMakeLists.txt";
            if (!File.Exists(cmakeListsPath))
            {
                Serilog.Log.Warning("CMakeLists.txt not found at {Path}. Skipping native build.", cmakeListsPath);
                return;
            }

            // Execute custom pre-build commands
            ExecuteCustomCommands(nativeConfig.CustomCommands, sourceDir);

            var vcpkgToolchain = nativeConfig.AutoDetectVcpkg
                ? TryDetectVcpkgToolchain(UnifyConfig.RepoRoot)
                : null;
            var hasPreset = HasCMakePresets(sourceDir);

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

    private static bool IsPlatformMatch(string? platform)
    {
        if (string.IsNullOrEmpty(platform))
            return true;

        return platform.ToLowerInvariant() switch
        {
            "windows" => RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
            "linux" => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
            "macos" or "osx" => RuntimeInformation.IsOSPlatform(OSPlatform.OSX),
            _ => true
        };
    }

    private static void ExecuteCustomCommands(string[] commands, AbsolutePath workingDir)
    {
        if (commands.Length == 0)
            return;

        foreach (var command in commands)
        {
            if (string.IsNullOrWhiteSpace(command))
                continue;

            Serilog.Log.Information("Executing custom command: {Command}", command);

            string executable;
            string arguments;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                executable = "cmd";
                arguments = $"/c {command}";
            }
            else
            {
                executable = "/bin/sh";
                arguments = $"-c \"{command.Replace("\"", "\\\"")}\"";
            }

            ProcessTasks.StartProcess(executable, arguments, workingDir)
                .AssertZeroExitCode();
        }
    }

    private static bool HasCMakePresets(AbsolutePath sourceDir)
    {
        var presetsPath = sourceDir / "CMakePresets.json";
        return File.Exists(presetsPath);
    }

    /// <summary>
    /// Attempts to detect the vcpkg toolchain file by checking multiple locations:
    /// 1. VCPKG_ROOT environment variable
    /// 2. Repository-local vcpkg directory
    /// 3. Common system install locations
    /// </summary>
    internal static string? TryDetectVcpkgToolchain(AbsolutePath repoRoot)
    {
        // 1. Check VCPKG_ROOT environment variable
        var vcpkgRoot = Environment.GetEnvironmentVariable("VCPKG_ROOT");
        if (!string.IsNullOrEmpty(vcpkgRoot))
        {
            var envToolchain = Path.Combine(vcpkgRoot, "scripts", "buildsystems", "vcpkg.cmake");
            if (File.Exists(envToolchain))
            {
                Serilog.Log.Debug("Detected vcpkg toolchain via VCPKG_ROOT: {Path}", envToolchain);
                return envToolchain;
            }
        }

        // 2. Check repo-local vcpkg directory (existing behavior)
        var repoToolchain = repoRoot / "vcpkg" / "scripts" / "buildsystems" / "vcpkg.cmake";
        if (File.Exists(repoToolchain))
        {
            Serilog.Log.Debug("Detected vcpkg toolchain in repo: {Path}", repoToolchain);
            return repoToolchain;
        }

        // 3. Check common system install locations
        var commonPaths = GetCommonVcpkgPaths();
        foreach (var basePath in commonPaths)
        {
            var toolchain = Path.Combine(basePath, "scripts", "buildsystems", "vcpkg.cmake");
            if (File.Exists(toolchain))
            {
                Serilog.Log.Debug("Detected vcpkg toolchain at common location: {Path}", toolchain);
                return toolchain;
            }
        }

        return null;
    }

    private static string[] GetCommonVcpkgPaths()
    {
        var paths = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            paths.Add(@"C:\vcpkg");
            paths.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "vcpkg"));
        }
        else
        {
            paths.Add("/usr/local/share/vcpkg");
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            paths.Add(Path.Combine(home, ".vcpkg"));
            paths.Add(Path.Combine(home, "vcpkg"));
        }

        return paths.ToArray();
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

        var sourceDir = config.CMakeSourceDir
            ?? throw new InvalidOperationException("Native build context is missing CMakeSourceDir.");
        var buildDir = config.CMakeBuildDir
            ?? throw new InvalidOperationException("Native build context is missing CMakeBuildDir.");

        buildDir.CreateDirectory();

        // Configure
        var configureArgs = $"-S \"{sourceDir}\" -B \"{buildDir}\" -DCMAKE_BUILD_TYPE={config.BuildConfig}";

        if (!string.IsNullOrEmpty(vcpkgToolchain))
        {
            Serilog.Log.Information("Using vcpkg toolchain: {Path}", vcpkgToolchain);
            configureArgs += $" -DCMAKE_TOOLCHAIN_FILE=\"{vcpkgToolchain}\"";
        }

        foreach (var option in config.CMakeOptions)
        {
            configureArgs += $" {option}";
        }

        ProcessTasks.StartProcess("cmake", configureArgs, sourceDir)
            .AssertZeroExitCode();

        // Build
        var buildArgs = $"--build \"{buildDir}\" --config {config.BuildConfig}";
        ProcessTasks.StartProcess("cmake", buildArgs, sourceDir)
            .AssertZeroExitCode();
    }

    private void CollectArtifacts(NativeBuildContext config)
    {
        if (config.OutputDir is null)
        {
            Serilog.Log.Debug("No output directory configured for native artifacts");
            return;
        }

        var buildDir = config.CMakeBuildDir
            ?? throw new InvalidOperationException("Native build context is missing CMakeBuildDir.");

        config.OutputDir.CreateDirectory();

        var buildOutputDir = buildDir / config.BuildConfig;
        if (!Directory.Exists(buildOutputDir))
        {
            buildOutputDir = buildDir;
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
