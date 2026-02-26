using FluentAssertions;
using Nuke.Common.IO;
using UnifyBuild.Nuke.Tests.Fixtures;
using Xunit;

namespace UnifyBuild.Nuke.Tests.Unit;

public class BuildContextLoaderTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();
    private AbsolutePath RepoRoot => (AbsolutePath)_temp.Path;
    public void Dispose() => _temp.Dispose();

    [Fact]
    public void FromJson_ValidConfig_LoadsSuccessfully()
    {
        _temp.CreateFile("build.config.json", TestConfigFixtures.ValidConfigJson);
        _temp.CreateDirectory("src/libs");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.Should().NotBeNull();
        ctx.Version.Should().Be("2.0.0");
        ctx.Solution.Should().NotBeNull();
    }

    [Fact]
    public void FromJson_ConfigInBuildDir_LoadsSuccessfully()
    {
        _temp.CreateFile("build/build.config.json", TestConfigFixtures.ValidConfigJson);
        _temp.CreateDirectory("src/libs");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.Should().NotBeNull();
        ctx.Version.Should().Be("2.0.0");
    }

    [Fact]
    public void FromJson_MalformedJson_Throws()
    {
        _temp.CreateFile("build.config.json", "{\"projectGroups\": { INVALID }");
        var act = () => BuildContextLoader.FromJson(RepoRoot);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void FromJson_MissingFile_ThrowsInvalidOperationException()
    {
        var act = () => BuildContextLoader.FromJson(RepoRoot);
        act.Should().Throw<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public void FromJson_EmptyFile_Throws()
    {
        _temp.CreateFile("build.config.json", "");
        var act = () => BuildContextLoader.FromJson(RepoRoot);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FromJson_JsonWithoutProjectGroups_ThrowsWithSchemaMessage()
    {
        _temp.CreateFile("build.config.json", "{\"version\": \"1.0.0\"}");
        var act = () => BuildContextLoader.FromJson(RepoRoot);
        act.Should().Throw<InvalidOperationException>().WithMessage("*projectGroups*");
    }

    [Fact]
    public void FromJson_ExplicitVersion_UsesConfigVersion()
    {
        _temp.CreateFile("build.config.json", "{\"version\": \"3.5.0\", \"projectGroups\": {}}");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.Version.Should().Be("3.5.0");
    }

    [Fact]
    public void FromJson_ExternalVersion_UsedWhenConfigVersionNull()
    {
        _temp.CreateFile("build.config.json", "{\"versionEnv\": null, \"projectGroups\": {}}");
        var ctx = BuildContextLoader.FromJson(RepoRoot, "build.config.json", "4.0.0-ext");
        ctx.Version.Should().Be("4.0.0-ext");
    }

    [Fact]
    public void FromJson_ArtifactsVersionFallback_UsedWhenOthersNull()
    {
        _temp.CreateFile("build.config.json", "{\"versionEnv\": null, \"artifactsVersion\": \"9.9.9\", \"projectGroups\": {}}");
        var ctx = BuildContextLoader.FromJson(RepoRoot, "build.config.json", null);
        ctx.Version.Should().Be("9.9.9");
    }

    [Fact]
    public void FromJson_NoVersionAnywhere_DefaultsTo010()
    {
        _temp.CreateFile("build.config.json", "{\"versionEnv\": null, \"projectGroups\": {}}");
        var ctx = BuildContextLoader.FromJson(RepoRoot, "build.config.json", null);
        ctx.Version.Should().Be("0.1.0");
    }

    [Fact]
    public void FromJson_ConfigVersionTakesPrecedenceOverExternal()
    {
        _temp.CreateFile("build.config.json", "{\"version\": \"1.0.0\", \"projectGroups\": {}}");
        var ctx = BuildContextLoader.FromJson(RepoRoot, "build.config.json", "2.0.0-ext");
        ctx.Version.Should().Be("1.0.0");
    }

    [Theory]
    [InlineData("publish")]
    [InlineData("pack")]
    [InlineData("compile")]
    public void FromJson_ProjectGroupAction_DiscoversCsprojFiles(string action)
    {
        var json = "{\"projectGroups\": {\"mygroup\": {\"sourceDir\": \"src/mygroup\", \"action\": \"" + action + "\"}}}";
        _temp.CreateFile("build.config.json", json);
        _temp.CreateFile("src/mygroup/ProjA/ProjA.csproj", "<Project />");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        switch (action)
        {
            case "publish":
                ctx.PublishProjects.Should().ContainSingle(p => p.Contains("ProjA"));
                break;
            case "pack":
                ctx.PackProjects.Should().ContainSingle(p => p.Contains("ProjA"));
                break;
            case "compile":
                ctx.CompileProjects.Should().ContainSingle(p => p.Contains("ProjA"));
                break;
        }
    }

    [Fact]
    public void FromJson_UnknownAction_DefaultsToCompile()
    {
        _temp.CreateFile("build.config.json", "{\"projectGroups\": {\"mygroup\": {\"sourceDir\": \"src/mygroup\", \"action\": \"unknown_action\"}}}");
        _temp.CreateFile("src/mygroup/Foo/Foo.csproj", "<Project />");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.CompileProjects.Should().ContainSingle(p => p.Contains("Foo"));
    }

    [Fact]
    public void FromJson_IncludeFilter_OnlyIncludesSpecifiedProjects()
    {
        _temp.CreateFile("build.config.json", "{\"projectGroups\": {\"libs\": {\"sourceDir\": \"src/libs\", \"action\": \"pack\", \"include\": [\"LibA\"]}}}");
        _temp.CreateFile("src/libs/LibA/LibA.csproj", "<Project />");
        _temp.CreateFile("src/libs/LibB/LibB.csproj", "<Project />");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.PackProjects.Should().ContainSingle(p => p.Contains("LibA"));
        ctx.PackProjects.Should().NotContain(p => p.Contains("LibB"));
    }

    [Fact]
    public void FromJson_ExcludeFilter_ExcludesSpecifiedProjects()
    {
        _temp.CreateFile("build.config.json", "{\"projectGroups\": {\"libs\": {\"sourceDir\": \"src/libs\", \"action\": \"pack\", \"exclude\": [\"LibB\"]}}}");
        _temp.CreateFile("src/libs/LibA/LibA.csproj", "<Project />");
        _temp.CreateFile("src/libs/LibB/LibB.csproj", "<Project />");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.PackProjects.Should().ContainSingle(p => p.Contains("LibA"));
        ctx.PackProjects.Should().NotContain(p => p.Contains("LibB"));
    }

    [Fact]
    public void FromJson_NativeBuild_AutoDetectedWhenCMakeListsExists()
    {
        _temp.CreateFile("build.config.json", "{\"projectGroups\": {}}");
        _temp.CreateFile("native/CMakeLists.txt", "cmake_minimum_required(VERSION 3.20)");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.NativeBuild.Should().NotBeNull();
        ctx.NativeBuild!.Enabled.Should().BeTrue();
    }

    [Fact]
    public void FromJson_NativeBuild_NullWhenNoCMakeListsAndNoConfig()
    {
        _temp.CreateFile("build.config.json", "{\"projectGroups\": {}}");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.NativeBuild.Should().BeNull();
    }

    [Fact]
    public void FromJson_NativeBuild_DisabledExplicitly()
    {
        _temp.CreateFile("build.config.json", "{\"projectGroups\": {}, \"nativeBuild\": {\"enabled\": false}}");
        _temp.CreateFile("native/CMakeLists.txt", "cmake_minimum_required(VERSION 3.20)");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.NativeBuild.Should().BeNull();
    }

    [Fact]
    public void FromJson_UnityBuild_CreatesContext()
    {
        _temp.CreateFile("build.config.json", "{\"projectGroups\": {}, \"unityBuild\": {\"targetFramework\": \"netstandard2.1\", \"unityProjectRoot\": \"unity/MyGame\", \"packages\": [{\"packageName\": \"com.example.core\", \"scopedIndex\": \"scoped-1234\", \"sourceProjects\": [\"project/contracts/Core/Core.csproj\"]}]}}");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.UnityBuild.Should().NotBeNull();
        ctx.UnityBuild!.TargetFramework.Should().Be("netstandard2.1");
        ctx.UnityBuild.Packages.Should().HaveCount(1);
        ctx.UnityBuild.Packages[0].PackageName.Should().Be("com.example.core");
    }

    [Fact]
    public void FromJson_NoUnityBuild_ContextIsNull()
    {
        _temp.CreateFile("build.config.json", "{\"projectGroups\": {}}");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.UnityBuild.Should().BeNull();
    }

    [Theory]
    [InlineData("hosts", "src/hosts")]
    [InlineData("executables", "src/executables")]
    [InlineData("apps", "src/apps")]
    public void FromJson_WellKnownHostGroups_MappedToHostsDir(string groupName, string sourceDir)
    {
        var json = "{\"projectGroups\": {\"" + groupName + "\": {\"sourceDir\": \"" + sourceDir + "\", \"action\": \"publish\"}}}";
        _temp.CreateFile("build.config.json", json);
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.HostsDir.ToString().Should().EndWith(sourceDir.Replace("/", Path.DirectorySeparatorChar.ToString()));
    }

    [Theory]
    [InlineData("libraries", "src/libraries")]
    [InlineData("plugins", "src/plugins")]
    [InlineData("libs", "src/libs")]
    public void FromJson_WellKnownLibraryGroups_MappedToPluginsDir(string groupName, string sourceDir)
    {
        var json = "{\"projectGroups\": {\"" + groupName + "\": {\"sourceDir\": \"" + sourceDir + "\", \"action\": \"compile\"}}}";
        _temp.CreateFile("build.config.json", json);
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.PluginsDir.ToString().Should().EndWith(sourceDir.Replace("/", Path.DirectorySeparatorChar.ToString()));
    }

    [Theory]
    [InlineData("contracts", "src/contracts")]
    [InlineData("packages", "src/packages")]
    [InlineData("abstractions", "src/abstractions")]
    public void FromJson_WellKnownContractGroups_MappedToContractsDir(string groupName, string sourceDir)
    {
        var json = "{\"projectGroups\": {\"" + groupName + "\": {\"sourceDir\": \"" + sourceDir + "\", \"action\": \"pack\"}}}";
        _temp.CreateFile("build.config.json", json);
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.ContractsDir.Should().NotBeNull();
        ctx.ContractsDir!.ToString().Should().EndWith(sourceDir.Replace("/", Path.DirectorySeparatorChar.ToString()));
    }

    [Fact]
    public void FromJson_HostGroupIncludeExclude_MappedToV1Properties()
    {
        _temp.CreateFile("build.config.json", "{\"projectGroups\": {\"hosts\": {\"sourceDir\": \"src/hosts\", \"action\": \"publish\", \"include\": [\"AppA\"], \"exclude\": [\"AppB\"]}}}");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.IncludeHosts.Should().Contain("AppA");
        ctx.ExcludeHosts.Should().Contain("AppB");
    }

    [Fact]
    public void FromJson_DiscoverProjects_ExcludesBinAndObj()
    {
        _temp.CreateFile("build.config.json", "{\"projectGroups\": {\"libs\": {\"sourceDir\": \"src/libs\", \"action\": \"compile\"}}}");
        _temp.CreateFile("src/libs/MyLib/MyLib.csproj", "<Project />");
        _temp.CreateFile("src/libs/MyLib/bin/Debug/net8.0/MyLib.csproj", "<Project />");
        _temp.CreateFile("src/libs/MyLib/obj/Debug/net8.0/MyLib.csproj", "<Project />");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.CompileProjects.Should().HaveCount(1);
        ctx.CompileProjects[0].Should().NotContain("bin");
        ctx.CompileProjects[0].Should().NotContain("obj");
    }

    [Fact]
    public void FromJson_MissingSourceDir_SkipsGroupGracefully()
    {
        _temp.CreateFile("build.config.json", "{\"projectGroups\": {\"libs\": {\"sourceDir\": \"src/nonexistent\", \"action\": \"compile\"}}}");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.CompileProjects.Should().BeEmpty();
    }

    [Fact]
    public void FromJson_SolutionPath_ResolvedRelativeToRepoRoot()
    {
        _temp.CreateFile("build.config.json", "{\"solution\": \"src/MySolution.sln\", \"projectGroups\": {}}");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.Solution.Should().NotBeNull();
        ctx.Solution!.ToString().Should().EndWith("MySolution.sln");
    }

    [Fact]
    public void FromJson_CustomNuGetOutputDir_Resolved()
    {
        _temp.CreateFile("build.config.json", "{\"nuGetOutputDir\": \"output/nuget\", \"projectGroups\": {}}");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.NuGetOutputDir.Should().NotBeNull();
        ctx.NuGetOutputDir!.ToString().Should().Contain("nuget");
    }

    [Fact]
    public void FromJson_DefaultNuGetOutputDir_UsesArtifactsVersion()
    {
        _temp.CreateFile("build.config.json", "{\"version\": \"5.0.0\", \"projectGroups\": {}}");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.NuGetOutputDir.Should().NotBeNull();
        ctx.NuGetOutputDir!.ToString().Should().Contain("5.0.0");
        ctx.NuGetOutputDir!.ToString().Should().Contain("nuget");
    }

    [Fact]
    public void FromJson_PackProperties_Loaded()
    {
        _temp.CreateFile("build.config.json", "{\"packProperties\": {\"UseDevelopmentReferences\": \"false\"}, \"packIncludeSymbols\": true, \"projectGroups\": {}}");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.PackIncludeSymbols.Should().BeTrue();
        ctx.PackProperties.Should().ContainKey("UseDevelopmentReferences");
    }

    [Fact]
    public void FromJson_MultipleGroups_ProjectsRoutedCorrectly()
    {
        _temp.CreateFile("build.config.json", "{\"projectGroups\": {\"apps\": {\"sourceDir\": \"src/apps\", \"action\": \"publish\"}, \"libs\": {\"sourceDir\": \"src/libs\", \"action\": \"pack\"}, \"tools\": {\"sourceDir\": \"src/tools\", \"action\": \"compile\"}}}");
        _temp.CreateFile("src/apps/App1/App1.csproj", "<Project />");
        _temp.CreateFile("src/libs/Lib1/Lib1.csproj", "<Project />");
        _temp.CreateFile("src/tools/Tool1/Tool1.csproj", "<Project />");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.PublishProjects.Should().ContainSingle(p => p.Contains("App1"));
        ctx.PackProjects.Should().ContainSingle(p => p.Contains("Lib1"));
        ctx.CompileProjects.Should().ContainSingle(p => p.Contains("Tool1"));
    }

    [Fact]
    public void FromJson_EmptyProjectGroups_DefaultV1Paths()
    {
        _temp.CreateFile("build.config.json", "{\"projectGroups\": {}}");
        var ctx = BuildContextLoader.FromJson(RepoRoot);
        ctx.HostsDir.ToString().Should().Contain("hosts");
        ctx.PluginsDir.ToString().Should().Contain("plugins");
    }
}
