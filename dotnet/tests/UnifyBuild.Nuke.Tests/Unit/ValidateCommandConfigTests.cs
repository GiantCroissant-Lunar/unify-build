using FluentAssertions;
using Nuke.Common.IO;
using UnifyBuild.Nuke.Commands;
using UnifyBuild.Nuke.Tests.Fixtures;
using Xunit;

namespace UnifyBuild.Nuke.Tests.Unit;

public class ValidateCommandConfigTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();
    private AbsolutePath RepoRoot => (AbsolutePath)_temp.Path;

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Execute_ResolvedBuildDirConfig_ValidatesSuccessfully()
    {
        _temp.CreateFile("build/build.config.json", "{\"projectGroups\": {}}");
        var resolved = BuildConfigResolver.ResolveConfigPath(RepoRoot);

        var command = new ValidateCommand();
        var result = command.Execute(resolved, RepoRoot);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Execute_ExplicitBuildDirConfig_ValidatesSuccessfully()
    {
        var configPath = (AbsolutePath)_temp.CreateFile("build/build.config.json", "{\"projectGroups\": {}}");

        var command = new ValidateCommand();
        var result = command.Execute(configPath, RepoRoot);

        result.IsValid.Should().BeTrue();
    }
}
