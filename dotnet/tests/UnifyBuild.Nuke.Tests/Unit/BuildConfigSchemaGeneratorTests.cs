using System.IO;
using System.Text.Json;
using FluentAssertions;
using NJsonSchema;
using Xunit;

namespace UnifyBuild.Nuke.Tests.Unit;

public class BuildConfigSchemaGeneratorTests
{
    [Fact]
    public void Generate_IncludesEveryTopLevelConfigurationSection()
    {
        using var document = JsonDocument.Parse(BuildConfigSchemaGenerator.Generate());
        var properties = document.RootElement.GetProperty("properties");

        var expectedProperties = new[]
        {
            "$schema",
            "version",
            "projectGroups",
            "nativeBuild",
            "rustBuild",
            "goBuild",
            "unityBuild",
            "godotBuild",
            "mobileBuild",
            "unityExport",
            "packageManagement",
            "performance",
            "observability",
            "extensions",
        };

        foreach (var property in expectedProperties)
        {
            properties.TryGetProperty(property, out _)
                .Should().BeTrue($"the generated schema should include '{property}'");
        }
    }

    [Fact]
    public void Generate_UsesValidDraft4DefinitionsWithStableTypeNames()
    {
        using var document = JsonDocument.Parse(BuildConfigSchemaGenerator.Generate());
        var root = document.RootElement;

        root.GetProperty("$schema").GetString()
            .Should().Be("http://json-schema.org/draft-04/schema#");

        var definitions = root.GetProperty("definitions");
        definitions.TryGetProperty("ProjectGroup", out _).Should().BeTrue();
        definitions.TryGetProperty("NativeBuildConfig", out _).Should().BeTrue();
        definitions.TryGetProperty("UnityBuildJsonConfig", out _).Should().BeTrue();
    }

    [Fact]
    public void Generate_MatchesEveryCheckedInSchemaCopy()
    {
        var repositoryRoot = FindRepositoryRoot();
        var generatedSchema = BuildConfigSchemaGenerator.Generate();
        var schemaCopies = new[]
        {
            Path.Combine(repositoryRoot, "build", "build.config.schema.json"),
            Path.Combine(repositoryRoot, "vscode-extension", "schemas", "build.config.schema.json"),
            Path.Combine(repositoryRoot, "dotnet", "tests", "UnifyBuild.Package.Tests", "build.config.schema.json"),
        };

        foreach (var schemaCopy in schemaCopies)
        {
            File.ReadAllText(schemaCopy)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Should().Be(generatedSchema, $"{schemaCopy} should be generated from BuildJsonConfig");
        }
    }

    [Fact]
    public async Task Generate_ValidatesEveryPublicExample()
    {
        var repositoryRoot = FindRepositoryRoot();
        var schema = await JsonSchema.FromJsonAsync(BuildConfigSchemaGenerator.Generate());
        var exampleConfigs = Directory.GetFiles(
            Path.Combine(repositoryRoot, "examples"),
            "build.config.json",
            SearchOption.AllDirectories);

        exampleConfigs.Should().NotBeEmpty();
        foreach (var exampleConfig in exampleConfigs)
        {
            var errors = schema.Validate(await File.ReadAllTextAsync(exampleConfig));
            errors.Should().BeEmpty(
                $"{exampleConfig} should match the schema:{Environment.NewLine}"
                + string.Join(Environment.NewLine, errors.Select(error => $"{error.Path}: {error.Kind}")));
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "dotnet", "UnifyBuild.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the unify-build repository root.");
    }
}
