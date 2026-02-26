using System.Text.Json;
using FluentAssertions;
using UnifyBuild.Nuke.Tests.Fixtures;
using Xunit;

namespace UnifyBuild.Nuke.Tests.Unit;

/// <summary>
/// Tests JSON deserialization behavior of config model classes using System.Text.Json directly.
/// Validates: Requirements 6.2, 6.3, 6.11
/// </summary>
public class ConfigParsingTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    #region BuildJsonConfig Defaults

    [Fact]
    public void BuildJsonConfig_DefaultValues_AreCorrect()
    {
        var config = new BuildJsonConfig();

        config.Version.Should().BeNull();
        config.VersionEnv.Should().Be("Version");
        config.ArtifactsVersion.Should().BeNull();
        config.Solution.Should().BeNull();
        config.ProjectGroups.Should().BeNull();
        config.CompileProjects.Should().BeNull();
        config.PublishProjects.Should().BeNull();
        config.PackProjects.Should().BeNull();
        config.NuGetOutputDir.Should().BeNull();
        config.PublishOutputDir.Should().BeNull();
        config.PackProperties.Should().BeNull();
        config.PackIncludeSymbols.Should().BeFalse();
        config.SyncLocalNugetFeed.Should().BeFalse();
        config.NativeBuild.Should().BeNull();
        config.UnityBuild.Should().BeNull();
    }

    [Fact]
    public void BuildJsonConfig_EmptyJson_DeserializesWithDefaults()
    {
        var config = JsonSerializer.Deserialize<BuildJsonConfig>("{}", Options)!;

        config.VersionEnv.Should().Be("Version");
        config.PackIncludeSymbols.Should().BeFalse();
        config.SyncLocalNugetFeed.Should().BeFalse();
        config.ProjectGroups.Should().BeNull();
    }

    #endregion

    #region Missing Optional Properties

    [Fact]
    public void BuildJsonConfig_OnlyVersion_OtherPropertiesNull()
    {
        var json = """{"version": "1.0.0"}""";
        var config = JsonSerializer.Deserialize<BuildJsonConfig>(json, Options)!;

        config.Version.Should().Be("1.0.0");
        config.Solution.Should().BeNull();
        config.ProjectGroups.Should().BeNull();
        config.NativeBuild.Should().BeNull();
        config.UnityBuild.Should().BeNull();
    }

    [Fact]
    public void BuildJsonConfig_OnlyProjectGroups_OtherPropertiesDefault()
    {
        var json = """{"projectGroups": {"libs": {"sourceDir": "src/libs"}}}""";
        var config = JsonSerializer.Deserialize<BuildJsonConfig>(json, Options)!;

        config.ProjectGroups.Should().ContainKey("libs");
        config.Version.Should().BeNull();
        config.PackIncludeSymbols.Should().BeFalse();
    }

    #endregion

    #region Extra/Unknown Properties

    [Fact]
    public void BuildJsonConfig_ExtraProperties_IgnoredByDeserializer()
    {
        var json = """
        {
            "version": "1.0.0",
            "unknownProperty": "should be ignored",
            "anotherExtra": 42,
            "nested": { "deep": true }
        }
        """;
        var config = JsonSerializer.Deserialize<BuildJsonConfig>(json, Options)!;

        config.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void ProjectGroup_ExtraProperties_IgnoredByDeserializer()
    {
        var json = """{"sourceDir": "src", "action": "pack", "extraField": "ignored"}""";
        var group = JsonSerializer.Deserialize<ProjectGroup>(json, Options)!;

        group.SourceDir.Should().Be("src");
        group.Action.Should().Be("pack");
    }

    #endregion

    #region Type Mismatches

    [Fact]
    public void BuildJsonConfig_StringWhereArrayExpected_ThrowsJsonException()
    {
        var json = """{"compileProjects": "not-an-array"}""";
        var act = () => JsonSerializer.Deserialize<BuildJsonConfig>(json, Options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void BuildJsonConfig_NumberWhereStringExpected_ThrowsJsonException()
    {
        var json = """{"version": 123}""";
        var act = () => JsonSerializer.Deserialize<BuildJsonConfig>(json, Options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void BuildJsonConfig_StringWhereBoolExpected_ThrowsJsonException()
    {
        var json = """{"packIncludeSymbols": "yes"}""";
        var act = () => JsonSerializer.Deserialize<BuildJsonConfig>(json, Options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void BuildJsonConfig_ArrayWhereDictExpected_ThrowsJsonException()
    {
        var json = """{"projectGroups": ["not", "a", "dict"]}""";
        var act = () => JsonSerializer.Deserialize<BuildJsonConfig>(json, Options);

        act.Should().Throw<JsonException>();
    }

    #endregion

    #region ProjectGroup Deserialization

    [Fact]
    public void ProjectGroup_DefaultValues_AreCorrect()
    {
        var group = new ProjectGroup();

        group.SourceDir.Should().Be(string.Empty);
        group.Action.Should().Be("compile");
        group.Include.Should().BeNull();
        group.Exclude.Should().BeNull();
        group.OutputDir.Should().BeNull();
        group.Properties.Should().BeNull();
    }

    [Fact]
    public void ProjectGroup_FullDeserialization_AllPropertiesSet()
    {
        var json = """
        {
            "sourceDir": "src/libs",
            "action": "pack",
            "include": ["LibA", "LibB"],
            "exclude": ["LibC"],
            "outputDir": "output/libs",
            "properties": { "Configuration": "Release" }
        }
        """;
        var group = JsonSerializer.Deserialize<ProjectGroup>(json, Options)!;

        group.SourceDir.Should().Be("src/libs");
        group.Action.Should().Be("pack");
        group.Include.Should().BeEquivalentTo(new[] { "LibA", "LibB" });
        group.Exclude.Should().BeEquivalentTo(new[] { "LibC" });
        group.OutputDir.Should().Be("output/libs");
        group.Properties.Should().ContainKey("Configuration").WhoseValue.Should().Be("Release");
    }

    [Fact]
    public void ProjectGroup_EmptyJson_UsesDefaults()
    {
        var group = JsonSerializer.Deserialize<ProjectGroup>("{}", Options)!;

        group.SourceDir.Should().Be(string.Empty);
        group.Action.Should().Be("compile");
        group.Include.Should().BeNull();
        group.Exclude.Should().BeNull();
    }

    [Fact]
    public void ProjectGroup_NullIncludeExclude_RemainsNull()
    {
        var json = """{"sourceDir": "src", "include": null, "exclude": null}""";
        var group = JsonSerializer.Deserialize<ProjectGroup>(json, Options)!;

        group.Include.Should().BeNull();
        group.Exclude.Should().BeNull();
    }

    [Fact]
    public void ProjectGroup_EmptyArrays_AreEmpty()
    {
        var json = """{"include": [], "exclude": []}""";
        var group = JsonSerializer.Deserialize<ProjectGroup>(json, Options)!;

        group.Include.Should().BeEmpty();
        group.Exclude.Should().BeEmpty();
    }

    #endregion

    #region NativeBuildConfig Deserialization

    [Fact]
    public void NativeBuildConfig_DefaultValues_AreCorrect()
    {
        var config = new NativeBuildConfig();

        config.Enabled.Should().BeTrue();
        config.CMakeSourceDir.Should().BeNull();
        config.CMakeBuildDir.Should().BeNull();
        config.CMakePreset.Should().BeNull();
        config.CMakeOptions.Should().BeNull();
        config.BuildConfig.Should().BeNull();
        config.AutoDetectVcpkg.Should().BeTrue();
        config.OutputDir.Should().BeNull();
        config.ArtifactPatterns.Should().BeNull();
    }

    [Fact]
    public void NativeBuildConfig_FullDeserialization_AllPropertiesSet()
    {
        var json = """
        {
            "enabled": true,
            "cMakeSourceDir": "native",
            "cMakeBuildDir": "native/build",
            "cMakePreset": "default",
            "cMakeOptions": ["-DBUILD_TESTS=ON"],
            "buildConfig": "Release",
            "autoDetectVcpkg": false,
            "outputDir": "output/native",
            "artifactPatterns": ["*.dll", "*.so"]
        }
        """;
        var config = JsonSerializer.Deserialize<NativeBuildConfig>(json, Options)!;

        config.Enabled.Should().BeTrue();
        config.CMakeSourceDir.Should().Be("native");
        config.CMakeBuildDir.Should().Be("native/build");
        config.CMakePreset.Should().Be("default");
        config.CMakeOptions.Should().BeEquivalentTo(new[] { "-DBUILD_TESTS=ON" });
        config.BuildConfig.Should().Be("Release");
        config.AutoDetectVcpkg.Should().BeFalse();
        config.OutputDir.Should().Be("output/native");
        config.ArtifactPatterns.Should().BeEquivalentTo(new[] { "*.dll", "*.so" });
    }

    [Fact]
    public void NativeBuildConfig_EmptyJson_UsesDefaults()
    {
        var config = JsonSerializer.Deserialize<NativeBuildConfig>("{}", Options)!;

        config.Enabled.Should().BeTrue();
        config.AutoDetectVcpkg.Should().BeTrue();
    }

    [Fact]
    public void NativeBuildConfig_DisabledExplicitly()
    {
        var json = """{"enabled": false}""";
        var config = JsonSerializer.Deserialize<NativeBuildConfig>(json, Options)!;

        config.Enabled.Should().BeFalse();
    }

    #endregion

    #region UnityBuildJsonConfig Deserialization

    [Fact]
    public void UnityBuildJsonConfig_FullDeserialization()
    {
        var json = """
        {
            "targetFramework": "netstandard2.1",
            "unityProjectRoot": "unity/MyGame",
            "packages": [
                {
                    "packageName": "com.example.core",
                    "scopedIndex": "scoped-1234",
                    "sourceProjects": ["project/Core/Core.csproj"],
                    "dependencyDlls": ["Newtonsoft.Json"]
                }
            ]
        }
        """;
        var config = JsonSerializer.Deserialize<UnityBuildJsonConfig>(json, Options)!;

        config.TargetFramework.Should().Be("netstandard2.1");
        config.UnityProjectRoot.Should().Be("unity/MyGame");
        config.Packages.Should().HaveCount(1);
        config.Packages![0].PackageName.Should().Be("com.example.core");
        config.Packages[0].ScopedIndex.Should().Be("scoped-1234");
        config.Packages[0].SourceProjects.Should().Contain("project/Core/Core.csproj");
        config.Packages[0].DependencyDlls.Should().Contain("Newtonsoft.Json");
    }

    [Fact]
    public void UnityBuildJsonConfig_EmptyJson_UsesDefaults()
    {
        var config = JsonSerializer.Deserialize<UnityBuildJsonConfig>("{}", Options)!;

        config.TargetFramework.Should().BeNull();
        config.UnityProjectRoot.Should().Be("");
        config.Packages.Should().BeNull();
    }

    #endregion

    #region UnityPackageMappingConfig Deserialization

    [Fact]
    public void UnityPackageMappingConfig_FullDeserialization()
    {
        var json = """
        {
            "packageName": "com.example.contracts",
            "scopedIndex": "scoped-5678",
            "sourceProjects": ["project/contracts/Foo/Foo.csproj"],
            "sourceProjectGlobs": ["project/contracts/*"],
            "dependencyDlls": ["Lib1", "Lib2"]
        }
        """;
        var config = JsonSerializer.Deserialize<UnityPackageMappingConfig>(json, Options)!;

        config.PackageName.Should().Be("com.example.contracts");
        config.ScopedIndex.Should().Be("scoped-5678");
        config.SourceProjects.Should().BeEquivalentTo(new[] { "project/contracts/Foo/Foo.csproj" });
        config.SourceProjectGlobs.Should().BeEquivalentTo(new[] { "project/contracts/*" });
        config.DependencyDlls.Should().BeEquivalentTo(new[] { "Lib1", "Lib2" });
    }

    [Fact]
    public void UnityPackageMappingConfig_DefaultValues()
    {
        var config = new UnityPackageMappingConfig();

        config.PackageName.Should().Be("");
        config.ScopedIndex.Should().Be("");
        config.SourceProjects.Should().BeNull();
        config.SourceProjectGlobs.Should().BeNull();
        config.DependencyDlls.Should().BeNull();
    }

    #endregion

    #region Case Insensitivity

    [Theory]
    [InlineData("""{"Version": "1.0.0"}""", "1.0.0")]
    [InlineData("""{"version": "2.0.0"}""", "2.0.0")]
    [InlineData("""{"VERSION": "3.0.0"}""", "3.0.0")]
    [InlineData("""{"vErSiOn": "4.0.0"}""", "4.0.0")]
    public void BuildJsonConfig_CaseInsensitive_PropertyNames(string json, string expectedVersion)
    {
        var config = JsonSerializer.Deserialize<BuildJsonConfig>(json, Options)!;
        config.Version.Should().Be(expectedVersion);
    }

    [Theory]
    [InlineData("""{"SourceDir": "src"}""", "src")]
    [InlineData("""{"sourceDir": "libs"}""", "libs")]
    [InlineData("""{"SOURCEDIR": "apps"}""", "apps")]
    public void ProjectGroup_CaseInsensitive_PropertyNames(string json, string expectedDir)
    {
        var group = JsonSerializer.Deserialize<ProjectGroup>(json, Options)!;
        group.SourceDir.Should().Be(expectedDir);
    }

    #endregion

    #region Parameterized Build_Config Variations

    [Theory]
    [InlineData("""{"packIncludeSymbols": true}""", true)]
    [InlineData("""{"packIncludeSymbols": false}""", false)]
    public void BuildJsonConfig_PackIncludeSymbols_Variations(string json, bool expected)
    {
        var config = JsonSerializer.Deserialize<BuildJsonConfig>(json, Options)!;
        config.PackIncludeSymbols.Should().Be(expected);
    }

    [Theory]
    [InlineData("""{"syncLocalNugetFeed": true}""", true)]
    [InlineData("""{"syncLocalNugetFeed": false}""", false)]
    public void BuildJsonConfig_SyncLocalNugetFeed_Variations(string json, bool expected)
    {
        var config = JsonSerializer.Deserialize<BuildJsonConfig>(json, Options)!;
        config.SyncLocalNugetFeed.Should().Be(expected);
    }

    [Theory]
    [InlineData("""{"versionEnv": "MY_VERSION"}""", "MY_VERSION")]
    [InlineData("""{"versionEnv": null}""", null)]
    [InlineData("""{"versionEnv": ""}""", "")]
    public void BuildJsonConfig_VersionEnv_Variations(string json, string? expected)
    {
        var config = JsonSerializer.Deserialize<BuildJsonConfig>(json, Options)!;
        config.VersionEnv.Should().Be(expected);
    }

    [Theory]
    [InlineData("publish")]
    [InlineData("pack")]
    [InlineData("compile")]
    [InlineData("custom")]
    public void ProjectGroup_Action_Variations(string action)
    {
        var json = $$$"""{"action": "{{{action}}}"}""";
        var group = JsonSerializer.Deserialize<ProjectGroup>(json, Options)!;
        group.Action.Should().Be(action);
    }

    #endregion

    #region Null vs Empty Arrays

    [Fact]
    public void BuildJsonConfig_NullArrays_RemainNull()
    {
        var json = """{"compileProjects": null, "publishProjects": null, "packProjects": null}""";
        var config = JsonSerializer.Deserialize<BuildJsonConfig>(json, Options)!;

        config.CompileProjects.Should().BeNull();
        config.PublishProjects.Should().BeNull();
        config.PackProjects.Should().BeNull();
    }

    [Fact]
    public void BuildJsonConfig_EmptyArrays_AreEmpty()
    {
        var json = """{"compileProjects": [], "publishProjects": [], "packProjects": []}""";
        var config = JsonSerializer.Deserialize<BuildJsonConfig>(json, Options)!;

        config.CompileProjects.Should().BeEmpty();
        config.PublishProjects.Should().BeEmpty();
        config.PackProjects.Should().BeEmpty();
    }

    #endregion

    #region Complex Nested Config

    [Fact]
    public void BuildJsonConfig_FullConfig_DeserializesCorrectly()
    {
        var json = """
        {
            "version": "2.5.0",
            "versionEnv": "BUILD_VERSION",
            "artifactsVersion": "2.5.0",
            "solution": "src/All.sln",
            "projectGroups": {
                "apps": {
                    "sourceDir": "src/apps",
                    "action": "publish",
                    "include": ["App1"],
                    "exclude": ["App2"],
                    "outputDir": "out/apps",
                    "properties": { "RuntimeIdentifier": "win-x64" }
                },
                "libs": {
                    "sourceDir": "src/libs",
                    "action": "pack"
                }
            },
            "compileProjects": ["src/Shared/Shared.csproj"],
            "nuGetOutputDir": "output/nuget",
            "publishOutputDir": "output/publish",
            "packProperties": { "UseDev": "false" },
            "packIncludeSymbols": true,
            "syncLocalNugetFeed": true,
            "localNugetFeedRoot": "/feeds",
            "nativeBuild": {
                "enabled": true,
                "cMakeSourceDir": "native",
                "buildConfig": "Release"
            },
            "unityBuild": {
                "targetFramework": "netstandard2.1",
                "unityProjectRoot": "unity/Game",
                "packages": [
                    {
                        "packageName": "com.example.core",
                        "scopedIndex": "scoped-1",
                        "sourceProjects": ["project/Core/Core.csproj"]
                    }
                ]
            }
        }
        """;
        var config = JsonSerializer.Deserialize<BuildJsonConfig>(json, Options)!;

        config.Version.Should().Be("2.5.0");
        config.VersionEnv.Should().Be("BUILD_VERSION");
        config.ArtifactsVersion.Should().Be("2.5.0");
        config.Solution.Should().Be("src/All.sln");
        config.ProjectGroups.Should().HaveCount(2);
        config.ProjectGroups!["apps"].Action.Should().Be("publish");
        config.ProjectGroups["apps"].Include.Should().Contain("App1");
        config.ProjectGroups["apps"].Properties.Should().ContainKey("RuntimeIdentifier");
        config.ProjectGroups["libs"].Action.Should().Be("pack");
        config.CompileProjects.Should().Contain("src/Shared/Shared.csproj");
        config.NuGetOutputDir.Should().Be("output/nuget");
        config.PublishOutputDir.Should().Be("output/publish");
        config.PackProperties.Should().ContainKey("UseDev");
        config.PackIncludeSymbols.Should().BeTrue();
        config.SyncLocalNugetFeed.Should().BeTrue();
        config.LocalNugetFeedRoot.Should().Be("/feeds");
        config.NativeBuild.Should().NotBeNull();
        config.NativeBuild!.CMakeSourceDir.Should().Be("native");
        config.UnityBuild.Should().NotBeNull();
        config.UnityBuild!.Packages.Should().HaveCount(1);
    }

    [Fact]
    public void BuildJsonConfig_SchemaProperty_Ignored()
    {
        var json = """{"$schema": "build.config.schema.json", "version": "1.0.0"}""";
        var config = JsonSerializer.Deserialize<BuildJsonConfig>(json, Options)!;

        config.Version.Should().Be("1.0.0");
    }

    #endregion
}
