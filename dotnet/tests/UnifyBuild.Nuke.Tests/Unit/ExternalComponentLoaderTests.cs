using FluentAssertions;
using UnifyBuild.Nuke.Extensibility;
using UnifyBuild.Nuke.Tests.Fixtures;
using Xunit;

namespace UnifyBuild.Nuke.Tests.Unit;

public class ExternalComponentLoaderTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void LoadFromPaths_NonExistentPath_ReturnsEmptyWithError()
    {
        var result = ExternalComponentLoader.LoadFromPaths(
            _temp.Path,
            new[] { "nonexistent/path" });

        result.ComponentTypes.Should().BeEmpty();
        result.Errors.Should().ContainSingle(e => e.Contains("not found"));
    }

    [Fact]
    public void LoadFromPaths_EmptyDirectory_ReturnsEmpty()
    {
        _temp.CreateDirectory("build/plugins");

        var result = ExternalComponentLoader.LoadFromPaths(
            _temp.Path,
            new[] { "build/plugins" });

        result.ComponentTypes.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateComponentType_InterfaceType_ReturnsFalse()
    {
        ExternalComponentLoader.ValidateComponentType(typeof(IUnifyBuildConfig))
            .Should().BeFalse();
    }

    [Fact]
    public void ValidateComponentType_AbstractType_ReturnsFalse()
    {
        // Use an abstract class that doesn't implement IUnifyBuildConfig
        ExternalComponentLoader.ValidateComponentType(typeof(System.IO.Stream))
            .Should().BeFalse();
    }

    [Fact]
    public void ValidateComponentType_NonComponentType_ReturnsFalse()
    {
        ExternalComponentLoader.ValidateComponentType(typeof(string))
            .Should().BeFalse();
    }

    [Fact]
    public void LoadFromConfig_NullConfig_ReturnsEmpty()
    {
        var result = ExternalComponentLoader.LoadFromConfig(_temp.Path, null);

        result.ComponentTypes.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromConfig_NoPathsNoAutoLoad_ReturnsEmpty()
    {
        var config = new ExtensionsConfig
        {
            AutoLoadPlugins = false,
            PluginPaths = null
        };

        var result = ExternalComponentLoader.LoadFromConfig(_temp.Path, config);

        result.ComponentTypes.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromConfig_AutoLoadWithEmptyPluginDir_ReturnsEmpty()
    {
        _temp.CreateDirectory("build/plugins");
        var config = new ExtensionsConfig { AutoLoadPlugins = true };

        var result = ExternalComponentLoader.LoadFromConfig(_temp.Path, config);

        result.ComponentTypes.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromPaths_InvalidDllFile_ReturnsError()
    {
        _temp.CreateFile("build/plugins/fake.dll", "this is not a real assembly");

        var result = ExternalComponentLoader.LoadFromPaths(
            _temp.Path,
            new[] { "build/plugins/fake.dll" });

        result.ComponentTypes.Should().BeEmpty();
        result.Errors.Should().ContainSingle(e =>
            e.Contains("fake.dll"));
    }

    [Fact]
    public void LoadFromPaths_InvalidDllInDirectory_ReturnsError()
    {
        _temp.CreateFile("build/plugins/fake.dll", "not a real assembly");

        var result = ExternalComponentLoader.LoadFromPaths(
            _temp.Path,
            new[] { "build/plugins" });

        result.ComponentTypes.Should().BeEmpty();
        result.Errors.Should().ContainSingle(e =>
            e.Contains("fake.dll"));
    }
}
