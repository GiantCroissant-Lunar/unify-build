using FluentAssertions;
using Nuke.Common.IO;
using UnifyBuild.Nuke.Commands;
using UnifyBuild.Nuke.Tests.Fixtures;
using Xunit;

namespace UnifyBuild.Nuke.Tests.Unit;

/// <summary>
/// Unit tests for InitCommand covering project discovery, template generation,
/// overwrite protection, Execute method, DetectOutputType, and SerializeConfig.
/// Validates: Requirements 6.4, 6.5
/// </summary>
public class InitCommandTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();
    private readonly InitCommand _sut = new();
    private AbsolutePath RepoRoot => (AbsolutePath)_temp.Path;

    private const string ExeCsproj =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>";

    private const string LibCsproj =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>";

    public void Dispose() => _temp.Dispose();

    #region Project Discovery

    [Fact]
    public void DiscoverProjects_FindsCsprojFilesRecursively()
    {
        _temp.CreateFile("src/libs/LibA/LibA.csproj", LibCsproj);
        _temp.CreateFile("src/libs/LibB/LibB.csproj", LibCsproj);
        _temp.CreateFile("src/apps/App1/App1.csproj", ExeCsproj);

        var result = _sut.DiscoverProjects(RepoRoot);

        result.Should().HaveCount(3);
        result.Select(p => p.Name).Should().BeEquivalentTo("LibA", "LibB", "App1");
    }

    [Fact]
    public void DiscoverProjects_ExcludesBinObjGitNodeModules()
    {
        _temp.CreateFile("src/MyLib/MyLib.csproj", LibCsproj);
        _temp.CreateFile("src/MyLib/bin/Debug/net8.0/MyLib.csproj", LibCsproj);
        _temp.CreateFile("src/MyLib/obj/Debug/net8.0/MyLib.csproj", LibCsproj);
        _temp.CreateFile(".git/hooks/Hook.csproj", LibCsproj);
        _temp.CreateFile("node_modules/SomePackage/SomePackage.csproj", LibCsproj);

        var result = _sut.DiscoverProjects(RepoRoot);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("MyLib");
    }

    [Fact]
    public void DiscoverProjects_ReturnsCorrectMetadata()
    {
        _temp.CreateFile("src/apps/MyApp/MyApp.csproj", ExeCsproj);

        var result = _sut.DiscoverProjects(RepoRoot);

        result.Should().ContainSingle();
        var project = result[0];
        project.Name.Should().Be("MyApp");
        project.RelativePath.Should().Be("src/apps/MyApp/MyApp.csproj");
        project.ParentDirectory.Should().Be("src/apps/MyApp");
        project.OutputType.Should().Be("Exe");
    }

    [Fact]
    public void DiscoverProjects_EmptyDirectory_ReturnsEmptyList()
    {
        var result = _sut.DiscoverProjects(RepoRoot);

        result.Should().BeEmpty();
    }

    [Fact]
    public void DiscoverProjects_LibraryProject_DetectsLibraryOutputType()
    {
        _temp.CreateFile("src/Lib/Lib.csproj", LibCsproj);

        var result = _sut.DiscoverProjects(RepoRoot);

        result.Should().ContainSingle().Which.OutputType.Should().Be("Library");
    }

    #endregion

    #region Template Generation

    [Fact]
    public void GenerateFromTemplate_Library_GroupsUnderPackagesWithPackAction()
    {
        var projects = new List<DiscoveredProject>
        {
            new("full/path/LibA.csproj", "LibA", "src/libs/LibA/LibA.csproj", "src/libs/LibA", "Library"),
            new("full/path/LibB.csproj", "LibB", "src/libs/LibB/LibB.csproj", "src/libs/LibB", "Library"),
        };

        var config = _sut.GenerateFromTemplate("library", projects);

        config.ProjectGroups.Should().ContainKey("packages");
        config.ProjectGroups!["packages"].Action.Should().Be("pack");
        config.ProjectGroups["packages"].Include.Should().BeEquivalentTo("LibA", "LibB");
        config.PackIncludeSymbols.Should().BeTrue();
    }

    [Fact]
    public void GenerateFromTemplate_Application_GroupsExeUnderAppsAndLibsUnderLibraries()
    {
        // FullPath must point to real directories since GenerateApplicationTemplate walks up to find .sln
        var appPath = _temp.CreateFile("src/apps/App/App.csproj", ExeCsproj);
        var libPath = _temp.CreateFile("src/libs/Lib/Lib.csproj", LibCsproj);

        var projects = new List<DiscoveredProject>
        {
            new(appPath, "App", "src/apps/App/App.csproj", "src/apps/App", "Exe"),
            new(libPath, "Lib", "src/libs/Lib/Lib.csproj", "src/libs/Lib", "Library"),
        };

        var config = _sut.GenerateFromTemplate("application", projects);

        config.ProjectGroups.Should().ContainKey("apps");
        config.ProjectGroups!["apps"].Action.Should().Be("publish");
        config.ProjectGroups["apps"].Include.Should().Contain("App");

        config.ProjectGroups.Should().ContainKey("libraries");
        config.ProjectGroups["libraries"].Action.Should().Be("compile");
        config.ProjectGroups["libraries"].Include.Should().Contain("Lib");
    }

    [Fact]
    public void GenerateFromTemplate_UnknownTemplate_ThrowsArgumentException()
    {
        var projects = new List<DiscoveredProject>();

        var act = () => _sut.GenerateFromTemplate("unknown", projects);

        act.Should().Throw<ArgumentException>().WithMessage("*unknown*");
    }

    #endregion

    #region Overwrite Protection

    [Fact]
    public void Execute_ConfigExists_ForceIsFalse_ThrowsInvalidOperationException()
    {
        _temp.CreateFile("build.config.json", "{}");
        var options = new InitOptions(_temp.Path, Interactive: false, Template: null, Force: false);

        var act = () => _sut.Execute(RepoRoot, options);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Fact]
    public void Execute_ConfigExists_ForceIsTrue_Succeeds()
    {
        _temp.CreateFile("build.config.json", "{}");
        _temp.CreateFile("src/Lib/Lib.csproj", LibCsproj);
        var options = new InitOptions(_temp.Path, Interactive: false, Template: null, Force: true);

        var result = _sut.Execute(RepoRoot, options);

        result.Should().NotBeNull();
        result.ConfigPath.Should().Contain("build.config.json");
    }

    #endregion

    #region Execute Method

    [Fact]
    public void Execute_CreatesConfigFileAtExpectedPath()
    {
        _temp.CreateFile("src/Lib/Lib.csproj", LibCsproj);
        var options = new InitOptions(_temp.Path, Interactive: false, Template: null, Force: false);

        var result = _sut.Execute(RepoRoot, options);

        var expectedPath = Path.Combine(_temp.Path, "build.config.json");
        result.ConfigPath.Should().Be(expectedPath);
        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public void Execute_ReturnsInitResultWithCorrectData()
    {
        _temp.CreateFile("src/Lib/Lib.csproj", LibCsproj);
        var options = new InitOptions(_temp.Path, Interactive: false, Template: null, Force: false);

        var result = _sut.Execute(RepoRoot, options);

        result.ConfigPath.Should().NotBeNullOrEmpty();
        result.GeneratedConfig.Should().NotBeNull();
        result.DiscoveredProjects.Should().Contain("src/Lib/Lib.csproj");
    }

    [Fact]
    public void Execute_CreatesParentDirectoriesIfNeeded()
    {
        _temp.CreateFile("src/Lib/Lib.csproj", LibCsproj);
        var nestedOutput = Path.Combine(_temp.Path, "nested", "output");
        var options = new InitOptions(nestedOutput, Interactive: false, Template: null, Force: false);

        var result = _sut.Execute(RepoRoot, options);

        File.Exists(result.ConfigPath).Should().BeTrue();
        Directory.Exists(nestedOutput).Should().BeTrue();
    }

    [Fact]
    public void Execute_WithTemplate_UsesTemplateGeneration()
    {
        _temp.CreateFile("src/Lib/Lib.csproj", LibCsproj);
        var options = new InitOptions(_temp.Path, Interactive: false, Template: "library", Force: false);

        var result = _sut.Execute(RepoRoot, options);

        result.GeneratedConfig.ProjectGroups.Should().ContainKey("packages");
        result.GeneratedConfig.PackIncludeSymbols.Should().BeTrue();
    }

    #endregion

    #region DetectOutputType

    [Fact]
    public void DetectOutputType_ExeProject_ReturnsExe()
    {
        var path = _temp.CreateFile("App.csproj", ExeCsproj);

        var result = InitCommand.DetectOutputType(path);

        result.Should().Be("Exe");
    }

    [Fact]
    public void DetectOutputType_LibraryProject_ReturnsLibrary()
    {
        var path = _temp.CreateFile("Lib.csproj", LibCsproj);

        var result = InitCommand.DetectOutputType(path);

        result.Should().Be("Library");
    }

    [Fact]
    public void DetectOutputType_UnparseableCsproj_ReturnsLibrary()
    {
        var path = _temp.CreateFile("Bad.csproj", "this is not valid xml");

        var result = InitCommand.DetectOutputType(path);

        result.Should().Be("Library");
    }

    #endregion

    #region SerializeConfig

    [Fact]
    public void SerializeConfig_ContainsSchemaReference()
    {
        var config = new BuildJsonConfig();

        var result = _sut.SerializeConfig(config);

        result.Should().Contain("$schema");
        result.Should().Contain("build.config.schema.json");
    }

    [Fact]
    public void SerializeConfig_ContainsCommentHeader()
    {
        var config = new BuildJsonConfig();

        var result = _sut.SerializeConfig(config);

        result.Should().Contain("// UnifyBuild configuration file");
    }

    [Fact]
    public void SerializeConfig_UsesCamelCasePropertyNames()
    {
        var config = new BuildJsonConfig
        {
            PackIncludeSymbols = true,
            ProjectGroups = new Dictionary<string, ProjectGroup>
            {
                ["test"] = new ProjectGroup { SourceDir = "src", Action = "pack" }
            }
        };

        var result = _sut.SerializeConfig(config);

        result.Should().Contain("packIncludeSymbols");
        result.Should().Contain("projectGroups");
        result.Should().Contain("sourceDir");
    }

    [Fact]
    public void SerializeConfig_OmitsNullProperties()
    {
        var config = new BuildJsonConfig();

        var result = _sut.SerializeConfig(config);

        result.Should().NotContain("\"version\"");
        result.Should().NotContain("\"solution\"");
        result.Should().NotContain("\"nativeBuild\"");
    }

    #endregion

    #region GenerateFromDiscovery (via Execute with no template)

    [Fact]
    public void Execute_NoTemplate_GroupsProjectsByParentDirectory()
    {
        _temp.CreateFile("src/libs/LibA/LibA.csproj", LibCsproj);
        _temp.CreateFile("src/libs/LibB/LibB.csproj", LibCsproj);
        _temp.CreateFile("src/apps/App1/App1.csproj", ExeCsproj);
        var options = new InitOptions(_temp.Path, Interactive: false, Template: null, Force: false);

        var result = _sut.Execute(RepoRoot, options);

        result.GeneratedConfig.ProjectGroups.Should().NotBeNull();
        result.GeneratedConfig.ProjectGroups!.Count.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public void Execute_NoTemplate_ExeProjectsGetPublishAction()
    {
        _temp.CreateFile("src/apps/App1/App1.csproj", ExeCsproj);
        var options = new InitOptions(_temp.Path, Interactive: false, Template: null, Force: false);

        var result = _sut.Execute(RepoRoot, options);

        var groups = result.GeneratedConfig.ProjectGroups!;
        var groupWithApp = groups.Values.First(g => g.Include!.Contains("App1"));
        groupWithApp.Action.Should().Be("publish");
    }

    [Fact]
    public void Execute_NoTemplate_LibraryProjectsGetPackAction()
    {
        _temp.CreateFile("src/libs/Lib1/Lib1.csproj", LibCsproj);
        var options = new InitOptions(_temp.Path, Interactive: false, Template: null, Force: false);

        var result = _sut.Execute(RepoRoot, options);

        var groups = result.GeneratedConfig.ProjectGroups!;
        var groupWithLib = groups.Values.First(g => g.Include!.Contains("Lib1"));
        groupWithLib.Action.Should().Be("pack");
    }

    #endregion
}
