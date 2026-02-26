using System.Text.Json;
using FluentAssertions;
using Nuke.Common.IO;
using UnifyBuild.Nuke;
using Xunit;

namespace UnifyBuild.Integration.Tests;

/// <summary>
/// End-to-end integration tests for build configuration loading workflows.
/// Uses real file system operations with temporary directories.
///
/// Validates: Requirements 7.1, 7.2, 7.10, 7.11
/// </summary>
public class EndToEndBuildTests : IDisposable
{
    private readonly string _tempDir;

    public EndToEndBuildTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "UnifyBuildIntegration_" + Guid.NewGuid().ToString("N")[..8]);
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
    /// Loading a valid config with project groups produces a BuildContext
    /// with correct version, project lists, and directory mappings.
    /// </summary>
    [Fact]
    public void LoadValidConfig_ProducesBuildContextWithCorrectProperties()
    {
        // Arrange
        var repoRoot = (AbsolutePath)_tempDir;
        CreateMinimalProjectStructure(repoRoot);

        var config = new
        {
            version = "2.5.0",
            projectGroups = new Dictionary<string, object>
            {
                ["executables"] = new { sourceDir = "src/apps", action = "publish" },
                ["libraries"] = new { sourceDir = "src/libs", action = "pack" }
            }
        };

        WriteConfig(repoRoot, config);

        // Act
        var context = BuildContextLoader.FromJson(repoRoot);

        // Assert
        context.Version.Should().Be("2.5.0");
        context.RepoRoot.ToString().Should().Be(repoRoot.ToString());
        context.PublishProjects.Should().Contain(p => p.Contains("MyApp.csproj"));
        context.PackProjects.Should().Contain(p => p.Contains("MyLib.csproj"));
    }

    /// <summary>
    /// Loading config with multiple project groups discovers all projects
    /// in each group's source directory.
    /// </summary>
    [Fact]
    public void LoadConfigWithProjectGroups_DiscoversProjectsInEachGroup()
    {
        // Arrange
        var repoRoot = (AbsolutePath)_tempDir;

        // Create multiple projects in different groups
        CreateDirectory("src/apps/App1");
        CreateFile("src/apps/App1/App1.csproj", MinimalCsproj);
        CreateDirectory("src/apps/App2");
        CreateFile("src/apps/App2/App2.csproj", MinimalCsproj);
        CreateDirectory("src/libs/Lib1");
        CreateFile("src/libs/Lib1/Lib1.csproj", MinimalCsproj);

        var config = new
        {
            projectGroups = new Dictionary<string, object>
            {
                ["apps"] = new { sourceDir = "src/apps", action = "publish" },
                ["libs"] = new { sourceDir = "src/libs", action = "compile" }
            }
        };

        WriteConfig(repoRoot, config);

        // Act
        var context = BuildContextLoader.FromJson(repoRoot);

        // Assert
        context.PublishProjects.Should().HaveCount(2);
        context.PublishProjects.Should().Contain(p => p.Contains("App1.csproj"));
        context.PublishProjects.Should().Contain(p => p.Contains("App2.csproj"));
        context.CompileProjects.Should().HaveCount(1);
        context.CompileProjects.Should().Contain(p => p.Contains("Lib1.csproj"));
    }

    /// <summary>
    /// When a version environment variable is set, BuildContextLoader resolves
    /// the version from that variable.
    /// </summary>
    [Fact]
    public void LoadConfigWithVersionEnv_ResolvesVersionFromEnvironment()
    {
        // Arrange
        var repoRoot = (AbsolutePath)_tempDir;
        CreateMinimalProjectStructure(repoRoot);

        var envVarName = "UNIFYBUILD_TEST_VERSION_" + Guid.NewGuid().ToString("N")[..6];
        Environment.SetEnvironmentVariable(envVarName, "3.1.4");

        try
        {
            var config = new
            {
                versionEnv = envVarName,
                projectGroups = new Dictionary<string, object>
                {
                    ["executables"] = new { sourceDir = "src/apps", action = "publish" }
                }
            };

            WriteConfig(repoRoot, config);

            // Act
            var context = BuildContextLoader.FromJson(repoRoot);

            // Assert
            context.Version.Should().Be("3.1.4");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    /// <summary>
    /// Loading from a path where no config file exists throws an informative error.
    /// </summary>
    [Fact]
    public void LoadMissingConfig_ThrowsWithInformativeMessage()
    {
        // Arrange
        var repoRoot = (AbsolutePath)_tempDir;
        // No config file created

        // Act & Assert
        var act = () => BuildContextLoader.FromJson(repoRoot);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    /// <summary>
    /// Loading a config file with invalid JSON throws an appropriate error.
    /// </summary>
    [Fact]
    public void LoadInvalidJson_ThrowsJsonException()
    {
        // Arrange
        var repoRoot = (AbsolutePath)_tempDir;
        CreateFile("build.config.json", "{ invalid json content }}}");

        // Act & Assert
        var act = () => BuildContextLoader.FromJson(repoRoot);
        act.Should().Throw<Exception>();
    }

    #region Helpers

    private static readonly string MinimalCsproj = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    private void CreateMinimalProjectStructure(AbsolutePath repoRoot)
    {
        CreateDirectory("src/apps/MyApp");
        CreateFile("src/apps/MyApp/MyApp.csproj", MinimalCsproj);
        CreateDirectory("src/libs/MyLib");
        CreateFile("src/libs/MyLib/MyLib.csproj", MinimalCsproj);
    }

    private string CreateDirectory(string relativePath)
    {
        var fullPath = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    private string CreateFile(string relativePath, string content = "")
    {
        var fullPath = Path.Combine(_tempDir, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (dir is not null) Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    private void WriteConfig(AbsolutePath repoRoot, object config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(repoRoot, "build.config.json"), json);
    }

    #endregion
}
