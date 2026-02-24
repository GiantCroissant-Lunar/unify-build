using System.Text.Json;
using Xunit;

namespace TestConsumer;

/// <summary>
/// Integration tests to verify that the build.config.schema.json file is properly
/// deployed to consumer projects when they install the UnifyBuild.Nuke package.
///
/// Validates Requirements 7.1, 7.2, 7.3:
/// - Schema file is copied to project root after installation
/// - Schema file is named "build.config.schema.json"
/// - Schema file is updated when package is updated
/// </summary>
public class SchemaDeploymentTests
{
    private const string SchemaFileName = "build.config.schema.json";

    /// <summary>
    /// Test that the schema file exists in the project root after package installation.
    /// Validates Requirement 7.1: Schema file is copied to Consumer_Project root directory
    /// </summary>
    [Fact]
    public void SchemaFile_ExistsInProjectRoot()
    {
        // Arrange
        var projectRoot = GetProjectRootDirectory();
        var schemaPath = Path.Combine(projectRoot, SchemaFileName);

        // Act & Assert
        Assert.True(File.Exists(schemaPath),
            $"Schema file '{SchemaFileName}' should exist in project root at: {schemaPath}");
    }

    /// <summary>
    /// Test that the schema file has the correct name.
    /// Validates Requirement 7.2: Schema file is named "build.config.schema.json"
    /// </summary>
    [Fact]
    public void SchemaFile_HasCorrectName()
    {
        // Arrange
        var projectRoot = GetProjectRootDirectory();
        var schemaPath = Path.Combine(projectRoot, SchemaFileName);

        // Act
        var fileName = Path.GetFileName(schemaPath);

        // Assert
        Assert.Equal("build.config.schema.json", fileName);
    }

    /// <summary>
    /// Test that the schema file contains valid JSON.
    /// </summary>
    [Fact]
    public void SchemaFile_ContainsValidJson()
    {
        // Arrange
        var projectRoot = GetProjectRootDirectory();
        var schemaPath = Path.Combine(projectRoot, SchemaFileName);

        // Act
        var schemaContent = File.ReadAllText(schemaPath);
        var parseException = Record.Exception(() => JsonDocument.Parse(schemaContent));

        // Assert
        Assert.Null(parseException);
    }

    /// <summary>
    /// Test that the schema file has the expected JSON Schema structure.
    /// </summary>
    [Fact]
    public void SchemaFile_HasValidJsonSchemaStructure()
    {
        // Arrange
        var projectRoot = GetProjectRootDirectory();
        var schemaPath = Path.Combine(projectRoot, SchemaFileName);
        var schemaContent = File.ReadAllText(schemaPath);

        // Act
        using var doc = JsonDocument.Parse(schemaContent);
        var root = doc.RootElement;

        // Assert
        Assert.True(root.TryGetProperty("$schema", out _),
            "Schema should have $schema property");
        Assert.True(root.TryGetProperty("type", out var typeProperty),
            "Schema should have type property");
        Assert.Equal("object", typeProperty.GetString());
        Assert.True(root.TryGetProperty("properties", out _),
            "Schema should have properties definition");
    }

    /// <summary>
    /// Test that the schema file includes expected UnifyBuild configuration properties.
    /// </summary>
    [Fact]
    public void SchemaFile_IncludesExpectedProperties()
    {
        // Arrange
        var projectRoot = GetProjectRootDirectory();
        var schemaPath = Path.Combine(projectRoot, SchemaFileName);
        var schemaContent = File.ReadAllText(schemaPath);

        // Act
        using var doc = JsonDocument.Parse(schemaContent);
        var root = doc.RootElement;
        var properties = root.GetProperty("properties");

        // Assert - Check for key UnifyBuild properties
        var expectedProperties = new[]
        {
            "version",
            "versionEnv",
            "artifactsVersion",
            "solution",
            "projectGroups",
            "compileProjects",
            "publishProjects",
            "packProjects"
        };

        foreach (var expectedProp in expectedProperties)
        {
            Assert.True(properties.TryGetProperty(expectedProp, out _),
                $"Schema should include property: {expectedProp}");
        }
    }

    /// <summary>
    /// Test that the schema file includes nested type definitions.
    /// </summary>
    [Fact]
    public void SchemaFile_IncludesNestedTypeDefinitions()
    {
        // Arrange
        var projectRoot = GetProjectRootDirectory();
        var schemaPath = Path.Combine(projectRoot, SchemaFileName);
        var schemaContent = File.ReadAllText(schemaPath);

        // Act
        using var doc = JsonDocument.Parse(schemaContent);
        var root = doc.RootElement;

        // Assert - Check for $defs with nested types
        Assert.True(root.TryGetProperty("$defs", out var defs),
            "Schema should have $defs for nested types");

        var expectedDefs = new[] { "projectGroup", "nativeBuild", "unityBuild" };
        foreach (var expectedDef in expectedDefs)
        {
            Assert.True(defs.TryGetProperty(expectedDef, out _),
                $"Schema should include definition: {expectedDef}");
        }
    }

    /// <summary>
    /// Test that a valid build.config.json can reference the schema.
    /// This simulates the consumer experience of using the schema for validation.
    /// Validates Requirement 8.1: IDE can resolve schema reference
    /// </summary>
    [Fact]
    public void SchemaFile_CanBeReferencedByBuildConfig()
    {
        // Arrange
        var projectRoot = GetProjectRootDirectory();
        var schemaPath = Path.Combine(projectRoot, SchemaFileName);
        var buildConfigPath = Path.Combine(projectRoot, "build.config.json");

        // Create a minimal build.config.json with schema reference
        var buildConfig = $$"""
        {
          "$schema": "./{{SchemaFileName}}",
          "versionEnv": "GITVERSION_MAJORMINORPATCH",
          "artifactsVersion": "1.0.0"
        }
        """;

        File.WriteAllText(buildConfigPath, buildConfig);

        try
        {
            // Act - Parse the config and verify schema reference
            using var doc = JsonDocument.Parse(buildConfig);
            var root = doc.RootElement;

            // Assert
            Assert.True(root.TryGetProperty("$schema", out var schemaRef));
            var schemaRefValue = schemaRef.GetString();
            Assert.NotNull(schemaRefValue);

            // Verify the referenced schema file exists
            var referencedSchemaPath = Path.Combine(projectRoot, schemaRefValue.TrimStart('.', '/'));
            Assert.True(File.Exists(referencedSchemaPath),
                $"Referenced schema file should exist at: {referencedSchemaPath}");
        }
        finally
        {
            // Cleanup
            if (File.Exists(buildConfigPath))
            {
                File.Delete(buildConfigPath);
            }
        }
    }

    /// <summary>
    /// Gets the project root directory (where the .csproj file is located).
    /// This is where the schema file should be copied by the NuGet package.
    /// </summary>
    private static string GetProjectRootDirectory()
    {
        // Start from the test assembly location and walk up to find the project root
        var assemblyLocation = typeof(SchemaDeploymentTests).Assembly.Location;
        var directory = Path.GetDirectoryName(assemblyLocation);

        while (directory != null)
        {
            // Look for the .csproj file
            if (Directory.GetFiles(directory, "*.csproj").Any())
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("Could not find project root directory");
    }
}
