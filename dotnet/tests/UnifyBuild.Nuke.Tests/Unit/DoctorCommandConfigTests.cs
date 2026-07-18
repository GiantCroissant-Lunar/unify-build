using System.IO;
using System.Linq;
using FluentAssertions;
using Nuke.Common.IO;
using UnifyBuild.Nuke.Commands;
using UnifyBuild.Nuke.Tests.Fixtures;
using Xunit;

namespace UnifyBuild.Nuke.Tests.Unit;

public class DoctorCommandConfigTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();
    private AbsolutePath RepoRoot => (AbsolutePath)_temp.Path;

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Execute_BuildDirConfig_PassesConfigExistsCheck()
    {
        var configPath = (AbsolutePath)_temp.CreateFile("build/build.config.json", "{\"projectGroups\": {}}");
        var command = new DoctorCommand();

        var result = command.Execute(RepoRoot, false, configPath);

        var configCheck = result.Checks.Single(c => c.Name == "build.config.json");
        configCheck.Status.Should().Be(DoctorStatus.Pass);
    }

    [Fact]
    public void Execute_BuildDirConfig_DoesNotCreateRootConfig()
    {
        var configPath = (AbsolutePath)_temp.CreateFile("build/build.config.json", "{\"projectGroups\": {}}");
        var command = new DoctorCommand();

        command.Execute(RepoRoot, false, configPath);

        File.Exists(RepoRoot / "build.config.json").Should().BeFalse();
    }
}
