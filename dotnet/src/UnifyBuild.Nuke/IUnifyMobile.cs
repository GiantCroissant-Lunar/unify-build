using System;
using System.Collections;
using System.Runtime.InteropServices;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Serilog;

namespace UnifyBuild.Nuke;

/// <summary>
/// Component for building and distributing mobile (iOS/Android) apps using Fastlane.
///
/// Supports two build modes:
/// 1. Standalone: builds from a native Gradle/Xcode project directly
/// 2. From exported project: builds from a Gradle/Xcode project exported by a game engine (Godot/Unity)
///
/// The unified pipeline for engine-based mobile builds:
///   Engine (Godot/Unity) exports native project → Fastlane builds + signs → Fastlane distributes
/// </summary>
public interface IUnifyMobile : IUnifyBuildConfig
{
    /// <summary>
    /// Install Ruby/Fastlane dependencies for configured mobile platforms.
    /// </summary>
    Target MobileRestore => _ => _
        .Description("Install Fastlane and Ruby dependencies for mobile projects")
        .Executes(() =>
        {
            var config = UnifyConfig.MobileBuild;
            if (config is null)
            {
                Log.Information("No mobile build configuration found. Skipping.");
                return;
            }

            if (config.Ios is not null)
                RunBundle("install", config.Ios.WorkingDir);

            if (config.Android is not null)
                RunBundle("install", config.Android.WorkingDir);
        });

    // ── Standalone builds (native projects) ──

    /// <summary>
    /// Build iOS app via Fastlane (standalone native project).
    /// </summary>
    Target MobileBuildIos => _ => _
        .Description("Build iOS app via Fastlane")
        .DependsOn(MobileRestore)
        .Executes(() =>
        {
            var platform = GetIosPlatform();
            if (platform is null) return;

            RunFastlane(platform.BuildLane, platform.WorkingDir, platform.EnvVars);
        });

    /// <summary>
    /// Build Android app via Fastlane (standalone native project).
    /// </summary>
    Target MobileBuildAndroid => _ => _
        .Description("Build Android app via Fastlane")
        .DependsOn(MobileRestore)
        .Executes(() =>
        {
            var platform = GetAndroidPlatform();
            if (platform is null) return;

            RunFastlane(platform.BuildLane, platform.WorkingDir, platform.EnvVars);
        });

    // ── Engine export builds (Godot/Unity exported Gradle/Xcode projects) ──

    /// <summary>
    /// Build Android APK/AAB from an engine-exported Gradle project via Fastlane.
    /// Set EXPORTED_PROJECT_DIR env var to the exported Gradle project path.
    /// </summary>
    Target MobileBuildAndroidFromProject => _ => _
        .Description("Build Android APK/AAB from engine-exported Gradle project via Fastlane")
        .DependsOn(MobileRestore)
        .Executes(() =>
        {
            var platform = GetAndroidPlatform();
            if (platform is null) return;

            RunFastlane(platform.BuildFromProjectLane, platform.WorkingDir, platform.EnvVars);
        });

    /// <summary>
    /// Build iOS IPA from an engine-exported Xcode project via Fastlane.
    /// Set EXPORTED_PROJECT_DIR env var to the exported Xcode project path.
    /// </summary>
    Target MobileBuildIosFromProject => _ => _
        .Description("Build iOS IPA from engine-exported Xcode project via Fastlane")
        .DependsOn(MobileRestore)
        .Executes(() =>
        {
            var platform = GetIosPlatform();
            if (platform is null) return;

            RunFastlane(platform.BuildFromProjectLane, platform.WorkingDir, platform.EnvVars);
        });

    // ── Distribution ──

    Target MobileDeployIosBeta => _ => _
        .Description("Deploy iOS build to TestFlight via Fastlane")
        .DependsOn(MobileRestore)
        .Executes(() =>
        {
            var platform = GetIosPlatform();
            if (platform is null) return;

            RunFastlane(platform.BetaLane, platform.WorkingDir, platform.EnvVars);
        });

    Target MobileDeployAndroidBeta => _ => _
        .Description("Deploy Android build to Play Store internal track via Fastlane")
        .DependsOn(MobileRestore)
        .Executes(() =>
        {
            var platform = GetAndroidPlatform();
            if (platform is null) return;

            RunFastlane(platform.BetaLane, platform.WorkingDir, platform.EnvVars);
        });

    Target MobileDeployIosRelease => _ => _
        .Description("Deploy iOS build to App Store via Fastlane")
        .DependsOn(MobileRestore)
        .Executes(() =>
        {
            var platform = GetIosPlatform();
            if (platform is null) return;

            RunFastlane(platform.ReleaseLane, platform.WorkingDir, platform.EnvVars);
        });

    Target MobileDeployAndroidRelease => _ => _
        .Description("Deploy Android build to Play Store production via Fastlane")
        .DependsOn(MobileRestore)
        .Executes(() =>
        {
            var platform = GetAndroidPlatform();
            if (platform is null) return;

            RunFastlane(platform.ReleaseLane, platform.WorkingDir, platform.EnvVars);
        });

    // --- helpers ---

    sealed MobilePlatformContext? GetIosPlatform()
    {
        var config = UnifyConfig.MobileBuild;
        if (config?.Ios is null)
        {
            Log.Information("No iOS configuration found. Skipping.");
            return null;
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Log.Warning("iOS builds require macOS. Current platform is not supported. Skipping.");
            return null;
        }

        return config.Ios;
    }

    sealed MobilePlatformContext? GetAndroidPlatform()
    {
        var config = UnifyConfig.MobileBuild;
        if (config?.Android is null)
        {
            Log.Information("No Android configuration found. Skipping.");
            return null;
        }

        return config.Android;
    }

    sealed void RunFastlane(string lane, AbsolutePath workingDir, Dictionary<string, string> envVars)
    {
        Log.Information("Running Fastlane lane '{Lane}' in {Dir}", lane, workingDir);

        // Build environment: inherit current process env (includes JAVA_HOME, ANDROID_HOME
        // set by InjectGradleWrapper) and layer on platform-specific env vars
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
            env[entry.Key!.ToString()!] = entry.Value?.ToString() ?? "";
        foreach (var kv in envVars)
            env[kv.Key] = kv.Value;

        var process = ProcessTasks.StartProcess(
            "bundle",
            $"exec fastlane {lane}",
            workingDirectory: workingDir,
            environmentVariables: env.AsReadOnly());

        process.AssertZeroExitCode();
    }

    sealed void RunBundle(string args, AbsolutePath workingDir)
    {
        Log.Information("Running bundle {Args} in {Dir}", args, workingDir);
        ProcessTasks.StartShell($"bundle {args}", workingDirectory: workingDir)
            .AssertZeroExitCode();
    }
}
