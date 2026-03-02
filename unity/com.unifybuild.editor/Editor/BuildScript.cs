using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnifyBuild.Editor
{
    /// <summary>
    /// Standardized build entry points for UnifyBuild CI/CD pipeline.
    /// Invoked via: Unity -batchmode -executeMethod UnifyBuild.Editor.BuildScript.Build
    /// 
    /// Configuration is read from environment variables set by IUnifyUnityExport:
    ///   UNIFYBUILD_TARGET  - BuildTarget name (e.g., "StandaloneWindows64", "Android", "iOS")
    ///   UNIFYBUILD_OUTPUT  - Output path for the build
    ///   UNIFYBUILD_VERSION - Version string (e.g., "1.2.3")
    /// </summary>
    public static class BuildScript
    {
        /// <summary>
        /// Main entry point called by IUnifyUnityExport via -executeMethod.
        /// </summary>
        public static void Build()
        {
            var target = GetEnvOrThrow("UNIFYBUILD_TARGET");
            var output = GetEnvOrThrow("UNIFYBUILD_OUTPUT");
            var version = GetEnvOrDefault("UNIFYBUILD_VERSION", "0.1.0");

            Debug.Log($"[UnifyBuild] Starting build: target={target}, output={output}, version={version}");

            var buildTarget = ParseBuildTarget(target);
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);

            // Set version
            PlayerSettings.bundleVersion = version;
            SetPlatformVersion(buildTargetGroup, version);

            var scenes = GetEnabledScenes();
            if (scenes.Length == 0)
            {
                Debug.LogWarning("[UnifyBuild] No enabled scenes found in Build Settings. Build may produce empty output.");
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = buildTarget,
                options = BuildOptions.None
            };

            // Apply platform-specific settings
            ConfigurePlatform(buildTarget, buildTargetGroup);

            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[UnifyBuild] Build failed: {report.summary.result}");
                Debug.LogError($"[UnifyBuild] Errors: {report.summary.totalErrors}, Warnings: {report.summary.totalWarnings}");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[UnifyBuild] Build succeeded: {report.summary.totalSize} bytes, {report.summary.totalTime}");
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Build for Android specifically. Convenience entry point.
        /// </summary>
        public static void BuildAndroid()
        {
            Environment.SetEnvironmentVariable("UNIFYBUILD_TARGET", "Android");
            Build();
        }

        /// <summary>
        /// Build for iOS specifically. Convenience entry point.
        /// </summary>
        public static void BuildIOS()
        {
            Environment.SetEnvironmentVariable("UNIFYBUILD_TARGET", "iOS");
            Build();
        }

        /// <summary>
        /// Build for Windows specifically. Convenience entry point.
        /// </summary>
        public static void BuildWindows()
        {
            Environment.SetEnvironmentVariable("UNIFYBUILD_TARGET", "StandaloneWindows64");
            Build();
        }

        /// <summary>
        /// Build for macOS specifically. Convenience entry point.
        /// </summary>
        public static void BuildMacOS()
        {
            Environment.SetEnvironmentVariable("UNIFYBUILD_TARGET", "StandaloneOSX");
            Build();
        }

        // --- private helpers ---

        private static BuildTarget ParseBuildTarget(string target)
        {
            if (Enum.TryParse<BuildTarget>(target, ignoreCase: true, out var result))
                return result;

            // Common aliases
            return target.ToLowerInvariant() switch
            {
                "windows" or "win64" => BuildTarget.StandaloneWindows64,
                "windows32" or "win32" => BuildTarget.StandaloneWindows,
                "macos" or "osx" or "mac" => BuildTarget.StandaloneOSX,
                "linux" or "linux64" => BuildTarget.StandaloneLinux64,
                "android" => BuildTarget.Android,
                "ios" or "iphone" => BuildTarget.iOS,
                "webgl" => BuildTarget.WebGL,
                _ => throw new ArgumentException($"Unknown build target: {target}")
            };
        }

        private static void SetPlatformVersion(BuildTargetGroup group, string version)
        {
            if (group == BuildTargetGroup.Android)
            {
                // Parse semver to Android version code
                var parts = version.Split('.');
                if (parts.Length >= 3
                    && int.TryParse(parts[0], out var major)
                    && int.TryParse(parts[1], out var minor)
                    && int.TryParse(parts[2], out var patch))
                {
                    PlayerSettings.Android.bundleVersionCode = major * 10000 + minor * 100 + patch;
                }
            }
            else if (group == BuildTargetGroup.iOS)
            {
                PlayerSettings.iOS.buildNumber = version;
            }
        }

        private static void ConfigurePlatform(BuildTarget target, BuildTargetGroup group)
        {
            if (target == BuildTarget.Android)
            {
                // Check if we should export a Gradle project instead of building APK/AAB directly
                var exportGradle = GetEnvOrDefault("UNIFYBUILD_ANDROID_EXPORT_GRADLE", "false");
                if (exportGradle.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    // Export as Gradle project for Fastlane to build
                    EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
                    Debug.Log("[UnifyBuild] Android: exporting as Gradle project for Fastlane build");
                }
                else
                {
                    EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

                    // Default to AAB for Play Store when building directly
                    var format = GetEnvOrDefault("UNIFYBUILD_ANDROID_FORMAT", "aab");
                    EditorUserBuildSettings.buildAppBundle = format.Equals("aab", StringComparison.OrdinalIgnoreCase);
                }

                var keystorePath = Environment.GetEnvironmentVariable("UNIFYBUILD_ANDROID_KEYSTORE");
                if (!string.IsNullOrEmpty(keystorePath))
                {
                    PlayerSettings.Android.keystoreName = keystorePath;
                    PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("UNIFYBUILD_ANDROID_KEYSTORE_PASS") ?? "";
                    PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("UNIFYBUILD_ANDROID_KEY_ALIAS") ?? "";
                    PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("UNIFYBUILD_ANDROID_KEY_ALIAS_PASS") ?? "";
                }
            }
        }

        private static string[] GetEnabledScenes()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
                .Select(s => s.path)
                .ToArray();
        }

        private static string GetEnvOrThrow(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value))
                throw new InvalidOperationException($"Required environment variable '{name}' is not set.");
            return value;
        }

        private static string GetEnvOrDefault(string name, string defaultValue)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }
    }
}
