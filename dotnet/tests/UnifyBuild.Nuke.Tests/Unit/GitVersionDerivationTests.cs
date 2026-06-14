using System.Diagnostics;
using FluentAssertions;
using Nuke.Common.IO;
using UnifyBuild.Nuke.Tests.Fixtures;
using Xunit;

namespace UnifyBuild.Nuke.Tests.Unit;

/// <summary>
/// Tests for <see cref="BuildContextLoader.TryDeriveVersionFromGit"/> — the git-derived
/// version fallback that keeps the build correct for any git repo even when the version
/// env is unset (instead of silently stamping 0.1.0). Each test stands up a throwaway git
/// repo under the system temp dir so the derivation runs against that repo only.
/// </summary>
public class GitVersionDerivationTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();
    private AbsolutePath RepoRoot => (AbsolutePath)_temp.Path;
    public void Dispose() => _temp.Dispose();

    [Fact]
    public void NonGitDirectory_ReturnsNull()
    {
        // A plain directory (not a work tree) must not derive a version — the caller
        // keeps its configured default. This also guarantees unit-test temp dirs and
        // non-git consumers stay on the 0.1.0 default path.
        BuildContextLoader.TryDeriveVersionFromGit(RepoRoot).Should().BeNull();
    }

    [Fact]
    public void TaggedRepoAheadOfTag_ReturnsPatchBumpedDescribeVersion()
    {
        InitRepo();
        Commit("initial");
        Git("tag v1.2.0");
        Commit("second"); // now one commit ahead of v1.2.0

        // git describe --tags --long --always -> "v1.2.0-1-g<sha>"
        // patch is bumped because the commits are ahead of the tagged patch.
        BuildContextLoader.TryDeriveVersionFromGit(RepoRoot).Should().Be("1.2.1-1");
    }

    [Fact]
    public void RepoWithoutVersionTag_ReturnsNull()
    {
        InitRepo();
        Commit("initial"); // no version tag -> describe yields a bare sha, no version

        BuildContextLoader.TryDeriveVersionFromGit(RepoRoot).Should().BeNull();
    }

    // --- git setup helpers ---

    private void InitRepo()
    {
        Git("init");
        // Local identity so commits succeed in CI without global git config.
        Git("config user.email tester@example.com");
        Git("config user.name Tester");
        Git("config commit.gpgsign false");
    }

    private void Commit(string message)
    {
        _temp.CreateFile($"{message}.txt", message);
        Git("add -A");
        Git($"commit -m {message}");
    }

    private void Git(string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-C \"{_temp.Path}\" {arguments}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit(15_000);
        process.ExitCode.Should().Be(0,
            "git {0} should succeed (stdout: {1}, stderr: {2})",
            arguments, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }
}
