using System.Text.Json;
using FluentAssertions;
using Nuke.Common.IO;
using UnifyBuild.Nuke;
using Xunit;

namespace UnifyBuild.Integration.Tests;

/// <summary>
/// End-to-end integration tests for pack configuration workflows.
/// Verifies that pack-related config properties are correctly loaded
/// into BuildContext using real file system operations.
///
/// Validates: Requirements 7.2, 7.3, 7.10, 7.11
/// </summary>
public class EndToEndPackTests : IDisposable
{
    private readonly string _tempDir;

    public EndToEndPackTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "UnifyBuildPackInteg_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    /// <summary>
    /// Config with a project group using "pack" action produces
    /// the correct PackProjects list in BuildContext.
    /// </summary>
    [Fact]
    public void ConfigWithPackAction_ProducesCorrectPackProjectsList()
    {
        // Arrange
        var repoRoot = (AbsolutePath)_tempDir;
        CreatePackageProject("src/packages/PkgA");
        CreatePackageProject("src/packages/PkgB");

        var config = new
        {
            projectGroups = new Dictionary<string, object>
            {
                ["packages"] = new { sourceDir = "src/packages", action = "pack" }
            }
        };

        WriteConfig(repoRoot, config);

        // Act
        var context = BuildContextLoader.FromJson(repoRoot);

        // Assert
        context.PackProjects.Should().HaveCount(2);
        context.PackProjects.Should().Contain(p => p.Contains("PkgA.csproj"));
        context.PackProjects.Should().Contain(p => p.Contains("PkgB.csproj"));
    }

    /// <summary>
    /// Config with NuGetOutputDir resolves the output directory
    /// relative to the repo root.
    /// </summary>
    [Fact]
    public void ConfigWithNuGetOutputDir_ResolvesCorrectly()
    {
        // Arrange
        var repoRoot = (AbsolutePath)_tempDir;
        CreatePackageProject("src/packages/PkgA");

        var config = new
        {
            nuGetOutputDir = "artifacts/nuget",
            projectGroups = new Dictionary<string, object>
            {
                ["packages"] = new { sourceDir = "src/packages", action = "pack" }
            }
        };

        WriteConfig(repoRoot, config);

        // Act
        var context = BuildContextLoader.FromJson(repoRoot);

        // Assert
        context.NuGetOutputDir.Should().NotBeNull();
        context.NuGetOutputDir!.ToString().Should().EndWith(Path.Combine("artifacts", "nuget"));
    }

    /// <summary>
    /// Config with pack properties passes them through to BuildContext.
    /// </summary>
    [Fact]
    public void ConfigWithPackProperties_PassesThroughToContext()
    {
        // Arrange
        var repoRoot = (AbsolutePath)_tempDir;
        CreatePackageProject("src/packages/PkgA");

        var config = new
        {
            packProperties = new Dictionary<string, string>
            {
                ["UseDevelopmentReferences"] = "false",
                ["ContinuousIntegrationBuild"] = "true"
            },
            projectGroups = new Dictionary<string, object>
            {
                ["packages"] = new { sourceDir = "src/packages", action = "pack" }
            }
        };

        WriteConfig(repoRoot, config);

        // Act
        var context = BuildContextLoader.FromJson(repoRoot);

        // Assert
        context.PackProperties.Should().ContainKey("UseDevelopmentReferences")
            .WhoseValue.Should().Be("false");
        context.PackProperties.Should().ContainKey("ContinuousIntegrationBuild")
            .WhoseValue.Should().Be("true");
    }

    /// <summary>
    /// Config with symbol packages enabled sets PackIncludeSymbols on BuildContext.
    /// </summary>
    [Fact]
    public void ConfigWithSymbolPackages_SetsPackIncludeSymbols()
    {
        // Arrange
        var repoRoot = (AbsolutePath)_tempDir;
        CreatePackageProject("src/packages/PkgA");

        var config = new
        {
            packIncludeSymbols = true,
            projectGroups = new Dictionary<string, object>
            {
                ["packages"] = new { sourceDir = "src/packages", action = "pack" }
            }
        };

        WriteConfig(repoRoot, config);

        // Act
        var context = BuildContextLoader.FromJson(repoRoot);

        // Assert
        context.PackIncludeSymbols.Should().BeTrue();
    }

    #region Helpers

    private static readonly string MinimalCsproj = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    private void CreatePackageProject(string relativePath)
    {
        var projectName = Path.GetFileName(relativePath);
        var fullDir = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(fullDir);
        File.WriteAllText(Path.Combine(fullDir, $"{projectName}.csproj"), MinimalCsproj);
    }

    private void WriteConfig(AbsolutePath repoRoot, object config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(repoRoot, "build.config.json"), json);
    }

    #endregion
}
