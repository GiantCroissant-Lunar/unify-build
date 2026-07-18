using System.IO;
using FluentAssertions;
using Nuke.Common.IO;
using UnifyBuild.Nuke.Tests.Fixtures;
using Xunit;

namespace UnifyBuild.Nuke.Tests.Unit;

public class BuildConfigResolverTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();
    private AbsolutePath RepoRoot => (AbsolutePath)_temp.Path;

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Resolve_OnlyBuildDirConfig_FindsBuildDirConfig()
    {
        var expected = _temp.CreateFile("build/build.config.json", "{\"projectGroups\": {}}");

        var resolved = BuildConfigResolver.ResolveConfigPath(RepoRoot);

        resolved.Should().Be((AbsolutePath)expected);
        File.Exists(resolved).Should().BeTrue();
    }

    [Fact]
    public void Resolve_RootConfigExists_PrefersRootOverBuildDir()
    {
        var rootConfig = _temp.CreateFile("build.config.json", "{\"projectGroups\": {}}");
        _temp.CreateFile("build/build.config.json", "{\"projectGroups\": {}}");

        var resolved = BuildConfigResolver.ResolveConfigPath(RepoRoot);

        resolved.Should().Be((AbsolutePath)rootConfig);
    }

    [Fact]
    public void Resolve_ExplicitRelativePath_Honored()
    {
        _temp.CreateFile("build.config.json", "{\"projectGroups\": {}}");
        var explicitConfig = _temp.CreateFile("custom/build.config.json", "{\"projectGroups\": {}}");

        var resolved = BuildConfigResolver.ResolveConfigPath(RepoRoot, "custom/build.config.json");

        resolved.Should().Be((AbsolutePath)explicitConfig);
    }

    [Fact]
    public void Resolve_ExplicitAbsolutePath_Honored()
    {
        _temp.CreateFile("build.config.json", "{\"projectGroups\": {}}");
        var explicitConfig = _temp.CreateFile("custom/build.config.json", "{\"projectGroups\": {}}");

        var resolved = BuildConfigResolver.ResolveConfigPath(RepoRoot, explicitConfig);

        resolved.Should().Be((AbsolutePath)explicitConfig);
    }

    [Fact]
    public void Resolve_MissingConfig_ReturnsRootCandidate()
    {
        var expected = RepoRoot / "build.config.json";

        var resolved = BuildConfigResolver.ResolveConfigPath(RepoRoot);

        resolved.Should().Be(expected);
        File.Exists(resolved).Should().BeFalse();
    }
}
