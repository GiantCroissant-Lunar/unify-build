using System.Collections.Generic;
using UnifyBuild.Nuke;

namespace UnifyBuild.Nuke.Tests.Fixtures;

/// <summary>
/// Provides shared sample <see cref="BuildJsonConfig"/> objects for use across tests.
/// </summary>
public static class TestConfigFixtures
{
    /// <summary>
    /// A valid, fully-populated configuration with multiple project groups.
    /// </summary>
    public static BuildJsonConfig ValidConfig => new()
    {
        Version = "1.0.0",
        VersionEnv = "Version",
        ArtifactsVersion = "1.0.0",
        Solution = "src/MySolution.sln",
        ProjectGroups = new Dictionary<string, ProjectGroup>
        {
            ["libraries"] = new ProjectGroup
            {
                SourceDir = "src/libs",
                Action = "pack",
                Include = new[] { "MyLib" },
                Exclude = null,
            },
            ["applications"] = new ProjectGroup
            {
                SourceDir = "src/apps",
                Action = "publish",
                Include = null,
                Exclude = new[] { "TestApp" },
            },
        },
        PackIncludeSymbols = true,
    };

    /// <summary>
    /// A minimal configuration with only required defaults.
    /// </summary>
    public static BuildJsonConfig MinimalConfig => new();

    /// <summary>
    /// A configuration with multiple project groups covering all action types.
    /// </summary>
    public static BuildJsonConfig MultiGroupConfig => new()
    {
        Solution = "All.sln",
        ProjectGroups = new Dictionary<string, ProjectGroup>
        {
            ["hosts"] = new ProjectGroup
            {
                SourceDir = "src/hosts",
                Action = "publish",
            },
            ["plugins"] = new ProjectGroup
            {
                SourceDir = "src/plugins",
                Action = "compile",
            },
            ["packages"] = new ProjectGroup
            {
                SourceDir = "src/packages",
                Action = "pack",
                Include = new[] { "PackageA", "PackageB" },
            },
        },
    };

    /// <summary>
    /// A configuration with native build settings.
    /// </summary>
    public static BuildJsonConfig NativeBuildConfig => new()
    {
        NativeBuild = new NativeBuildConfig
        {
            Enabled = true,
            CMakeSourceDir = "native",
            CMakeBuildDir = "native/build",
            BuildConfig = "Release",
            ArtifactPatterns = new[] { "*.dll", "*.so", "*.dylib" },
        },
    };

    /// <summary>
    /// A configuration with Unity build settings.
    /// </summary>
    public static BuildJsonConfig UnityBuildConfig => new()
    {
        UnityBuild = new UnityBuildJsonConfig
        {
            TargetFramework = "netstandard2.1",
            UnityProjectRoot = "unity/MyGame",
            Packages = new[]
            {
                new UnityPackageMappingConfig
                {
                    PackageName = "com.example.core",
                    ScopedIndex = "scoped-1234",
                    SourceProjects = new[] { "project/contracts/Core/Core.csproj" },
                },
            },
        },
    };

    /// <summary>
    /// A configuration with explicit project lists (legacy-style).
    /// </summary>
    public static BuildJsonConfig ExplicitProjectsConfig => new()
    {
        CompileProjects = new[] { "src/Shared/Shared.csproj" },
        PublishProjects = new[] { "src/App/App.csproj" },
        PackProjects = new[] { "src/Lib/Lib.csproj" },
    };

    /// <summary>
    /// Returns a valid JSON string representing a minimal build config.
    /// </summary>
    public static string MinimalConfigJson =>
        """
        {
          "$schema": "build.config.schema.json"
        }
        """;

    /// <summary>
    /// Returns a valid JSON string representing a config with project groups.
    /// </summary>
    public static string ValidConfigJson =>
        """
        {
          "$schema": "build.config.schema.json",
          "version": "2.0.0",
          "solution": "src/MySolution.sln",
          "projectGroups": {
            "libraries": {
              "sourceDir": "src/libs",
              "action": "pack"
            }
          }
        }
        """;
}
