using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Nuke.Common.IO;
using UnifyBuild.Nuke;
using UnifyBuild.Nuke.Performance;
using Xunit;

namespace UnifyBuild.Nuke.Tests.Unit;

public class ParallelBuildOrchestratorTests : IDisposable
{
    private readonly string _tempDir;

    public ParallelBuildOrchestratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"parallel-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void IndependentGroups_WithDifferentSourceDirs_AreInSameBatch()
    {
        // Arrange
        var repoRoot = (AbsolutePath)_tempDir;
        Directory.CreateDirectory(Path.Combine(_tempDir, "src", "apps"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "src", "libs"));

        var groups = new Dictionary<string, ProjectGroup>
        {
            ["apps"] = new ProjectGroup { SourceDir = "src/apps", Action = "publish" },
            ["libs"] = new ProjectGroup { SourceDir = "src/libs", Action = "pack" }
        };

        // Act
        var batches = ParallelBuildOrchestrator.AnalyzeIndependence(groups, repoRoot);

        // Assert — both groups should be in a single batch (independent)
        batches.Should().HaveCount(1);
        batches[0].Should().Contain("apps").And.Contain("libs");
    }

    [Fact]
    public void OverlappingGroups_WithSharedSourceDirs_AreInSeparateBatches()
    {
        // Arrange
        var repoRoot = (AbsolutePath)_tempDir;
        Directory.CreateDirectory(Path.Combine(_tempDir, "src"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "src", "sub"));

        var groups = new Dictionary<string, ProjectGroup>
        {
            ["parent"] = new ProjectGroup { SourceDir = "src", Action = "compile" },
            ["child"] = new ProjectGroup { SourceDir = "src/sub", Action = "pack" }
        };

        // Act
        var batches = ParallelBuildOrchestrator.AnalyzeIndependence(groups, repoRoot);

        // Assert — overlapping dirs means they must be in separate batches
        batches.Should().HaveCount(2);
    }

    [Fact]
    public void IdenticalSourceDirs_AreOverlapping()
    {
        // Arrange
        var repoRoot = (AbsolutePath)_tempDir;
        Directory.CreateDirectory(Path.Combine(_tempDir, "src"));

        var groups = new Dictionary<string, ProjectGroup>
        {
            ["groupA"] = new ProjectGroup { SourceDir = "src", Action = "compile" },
            ["groupB"] = new ProjectGroup { SourceDir = "src", Action = "pack" }
        };

        // Act
        var batches = ParallelBuildOrchestrator.AnalyzeIndependence(groups, repoRoot);

        // Assert — same dir means dependent
        batches.Should().HaveCount(2);
    }

    [Fact]
    public void EmptyGroups_ReturnsEmptyBatches()
    {
        var repoRoot = (AbsolutePath)_tempDir;
        var groups = new Dictionary<string, ProjectGroup>();

        var batches = ParallelBuildOrchestrator.AnalyzeIndependence(groups, repoRoot);

        batches.Should().BeEmpty();
    }

    [Fact]
    public void MaxParallelism_DefaultsToProcessorCount()
    {
        var orchestrator = new ParallelBuildOrchestrator();
        orchestrator.MaxParallelism.Should().Be(Environment.ProcessorCount);
    }

    [Fact]
    public void MaxParallelism_RespectsExplicitValue()
    {
        var orchestrator = new ParallelBuildOrchestrator(maxParallelism: 4);
        orchestrator.MaxParallelism.Should().Be(4);
    }

    [Fact]
    public void MaxParallelism_ZeroOrNegative_DefaultsToProcessorCount()
    {
        var orchestratorZero = new ParallelBuildOrchestrator(maxParallelism: 0);
        orchestratorZero.MaxParallelism.Should().Be(Environment.ProcessorCount);

        var orchestratorNeg = new ParallelBuildOrchestrator(maxParallelism: -1);
        orchestratorNeg.MaxParallelism.Should().Be(Environment.ProcessorCount);
    }

    [Fact]
    public async Task BuildAsync_ExecutesAllGroups()
    {
        // Arrange
        var repoRoot = (AbsolutePath)_tempDir;
        Directory.CreateDirectory(Path.Combine(_tempDir, "src", "a"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "src", "b"));

        var groups = new Dictionary<string, ProjectGroup>
        {
            ["a"] = new ProjectGroup { SourceDir = "src/a", Action = "compile" },
            ["b"] = new ProjectGroup { SourceDir = "src/b", Action = "compile" }
        };

        var executed = new List<string>();
        var orchestrator = new ParallelBuildOrchestrator(maxParallelism: 2);

        // Act
        var results = await orchestrator.BuildAsync(
            groups, repoRoot,
            async (name, group) =>
            {
                lock (executed) { executed.Add(name); }
                await Task.Delay(10);
                return true;
            });

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.Success);
        executed.Should().Contain("a").And.Contain("b");
    }

    [Fact]
    public async Task BuildAsync_CapturesFailure()
    {
        var repoRoot = (AbsolutePath)_tempDir;
        Directory.CreateDirectory(Path.Combine(_tempDir, "src", "fail"));

        var groups = new Dictionary<string, ProjectGroup>
        {
            ["fail"] = new ProjectGroup { SourceDir = "src/fail", Action = "compile" }
        };

        var orchestrator = new ParallelBuildOrchestrator();

        var results = await orchestrator.BuildAsync(
            groups, repoRoot,
            (name, group) => throw new InvalidOperationException("build failed"));

        results.Should().HaveCount(1);
        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Contain("build failed");
    }

    [Fact]
    public void DirectoriesOverlap_DetectsParentChild()
    {
        ParallelBuildOrchestrator.DirectoriesOverlap(
            Path.Combine(_tempDir, "src"),
            Path.Combine(_tempDir, "src", "sub")).Should().BeTrue();
    }

    [Fact]
    public void DirectoriesOverlap_SiblingDirsAreNotOverlapping()
    {
        ParallelBuildOrchestrator.DirectoriesOverlap(
            Path.Combine(_tempDir, "src", "a"),
            Path.Combine(_tempDir, "src", "b")).Should().BeFalse();
    }

    [Fact]
    public void ThreeIndependentGroups_AllInOneBatch()
    {
        var repoRoot = (AbsolutePath)_tempDir;
        Directory.CreateDirectory(Path.Combine(_tempDir, "a"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "b"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "c"));

        var groups = new Dictionary<string, ProjectGroup>
        {
            ["g1"] = new ProjectGroup { SourceDir = "a", Action = "compile" },
            ["g2"] = new ProjectGroup { SourceDir = "b", Action = "pack" },
            ["g3"] = new ProjectGroup { SourceDir = "c", Action = "publish" }
        };

        var batches = ParallelBuildOrchestrator.AnalyzeIndependence(groups, repoRoot);

        batches.Should().HaveCount(1);
        batches[0].Should().HaveCount(3);
    }
}
