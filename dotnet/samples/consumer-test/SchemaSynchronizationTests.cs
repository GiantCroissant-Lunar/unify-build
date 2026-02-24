using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace TestConsumer;

/// <summary>
/// Integration tests to verify that the JSON schema stays synchronized with BuildConfigJson.cs.
/// Tests that modifying the C# source and regenerating produces an updated schema.
///
/// **Property 10: Schema Synchronization with Source**
/// **Validates: Requirements 11.1, 11.2, 11.3, 11.4**
/// </summary>
public class SchemaSynchronizationTests : IDisposable
{
    private readonly string _testWorkingDir;
    private readonly string _originalSourceFile;
    private readonly string _tempSourceFile;
    private readonly string _tempSchemaFile;

    public SchemaSynchronizationTests()
    {
        // Set up temporary working directory for test files
        _testWorkingDir = Path.Combine(Path.GetTempPath(), $"schema-sync-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testWorkingDir);

        // Find the repository root (where the .git directory is)
        var repoRoot = FindRepositoryRoot();
        _originalSourceFile = Path.Combine(repoRoot, "dotnet", "src", "UnifyBuild.Nuke", "BuildConfigJson.cs");

        // Set up temp file paths
        _tempSourceFile = Path.Combine(_testWorkingDir, "BuildConfigJson.cs");
        _tempSchemaFile = Path.Combine(_testWorkingDir, "build.config.schema.json");
    }

    public void Dispose()
    {
        // Clean up temporary files
        if (Directory.Exists(_testWorkingDir))
        {
            try
            {
                Directory.Delete(_testWorkingDir, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    /// <summary>
    /// Test that adding a property to BuildConfigJson.cs results in the property
    /// appearing in the regenerated schema.
    /// Validates Requirement 11.2: Property additions are reflected in schema
    /// </summary>
    [Fact]
    public void SchemaRegeneration_WhenPropertyAdded_IncludesNewProperty()
    {
        // Skip if npx is not available
        if (!IsNpxAvailable())
        {
            return; // Skip test
        }

        // Arrange - Copy original source and add a new property
        var originalContent = File.ReadAllText(_originalSourceFile);
        File.WriteAllText(_tempSourceFile, originalContent);

        // Add a new property to the BuildJsonConfig class
        var modifiedContent = AddPropertyToClass(
            originalContent,
            "BuildJsonConfig",
            "public string? TestNewProperty { get; set; }"
        );
        File.WriteAllText(_tempSourceFile, modifiedContent);

        // Act - Generate schema from modified source
        GenerateSchemaFromSource(_tempSourceFile, _tempSchemaFile);

        // Assert - Verify the new property exists in the schema
        var schemaContent = File.ReadAllText(_tempSchemaFile);
        using var doc = JsonDocument.Parse(schemaContent);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("properties", out var properties),
            "Schema should have properties definition");
        Assert.True(properties.TryGetProperty("testNewProperty", out _),
            "Schema should include the newly added property 'testNewProperty'");
    }

    /// <summary>
    /// Test that removing a property from BuildConfigJson.cs results in the property
    /// being absent from the regenerated schema.
    /// Validates Requirement 11.3: Property removals are reflected in schema
    /// </summary>
    [Fact]
    public void SchemaRegeneration_WhenPropertyRemoved_ExcludesRemovedProperty()
    {
        // Skip if npx is not available
        if (!IsNpxAvailable())
        {
            return; // Skip test
        }

        // Arrange - Copy original source and remove a property
        var originalContent = File.ReadAllText(_originalSourceFile);

        // Remove the "Solution" property from BuildJsonConfig class
        var modifiedContent = RemovePropertyFromClass(
            originalContent,
            "BuildJsonConfig",
            "Solution"
        );
        File.WriteAllText(_tempSourceFile, modifiedContent);

        // Act - Generate schema from modified source
        GenerateSchemaFromSource(_tempSourceFile, _tempSchemaFile);

        // Assert - Verify the removed property does not exist in the schema
        var schemaContent = File.ReadAllText(_tempSchemaFile);
        using var doc = JsonDocument.Parse(schemaContent);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("properties", out var properties),
            "Schema should have properties definition");
        Assert.False(properties.TryGetProperty("solution", out _),
            "Schema should not include the removed property 'solution'");
    }

    /// <summary>
    /// Test that changing a property type in BuildConfigJson.cs results in the
    /// schema reflecting the new type.
    /// Validates Requirement 11.4: Property type changes are reflected in schema
    /// </summary>
    [Fact]
    public void SchemaRegeneration_WhenPropertyTypeChanged_ReflectsNewType()
    {
        // Skip if npx is not available
        if (!IsNpxAvailable())
        {
            return; // Skip test
        }

        // Arrange - Copy original source and change a property type
        var originalContent = File.ReadAllText(_originalSourceFile);

        // Change PackIncludeSymbols from bool to string
        var modifiedContent = ChangePropertyType(
            originalContent,
            "BuildJsonConfig",
            "PackIncludeSymbols",
            "bool",
            "string?"
        );
        File.WriteAllText(_tempSourceFile, modifiedContent);

        // Act - Generate schema from modified source
        GenerateSchemaFromSource(_tempSourceFile, _tempSchemaFile);

        // Assert - Verify the property type changed in the schema
        var schemaContent = File.ReadAllText(_tempSchemaFile);
        using var doc = JsonDocument.Parse(schemaContent);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("properties", out var properties),
            "Schema should have properties definition");
        Assert.True(properties.TryGetProperty("packIncludeSymbols", out var packIncludeSymbols),
            "Schema should include the 'packIncludeSymbols' property");

        // The property should now be a string type (or string/null for nullable)
        if (packIncludeSymbols.TryGetProperty("type", out var typeProperty))
        {
            var typeValue = typeProperty.ValueKind == JsonValueKind.Array
                ? string.Join(",", typeProperty.EnumerateArray().Select(t => t.GetString() ?? ""))
                : typeProperty.GetString();

            Assert.Contains("string", typeValue ?? "");
        }
        else if (packIncludeSymbols.TryGetProperty("anyOf", out var anyOf))
        {
            // Check if any of the anyOf options is a string type
            var hasStringType = false;
            foreach (var option in anyOf.EnumerateArray())
            {
                if (option.TryGetProperty("type", out var optionType) &&
                    optionType.GetString() == "string")
                {
                    hasStringType = true;
                    break;
                }
            }
            Assert.True(hasStringType, "Property should have string type in anyOf options");
        }
        else
        {
            Assert.Fail("Property should have either 'type' or 'anyOf' definition");
        }
    }

    /// <summary>
    /// Test that multiple modifications (add, remove, change) are all reflected
    /// in the regenerated schema.
    /// Validates Requirements 11.1, 11.2, 11.3, 11.4: Complete synchronization
    /// </summary>
    [Fact]
    public void SchemaRegeneration_WhenMultipleChanges_ReflectsAllChanges()
    {
        // Skip if npx is not available
        if (!IsNpxAvailable())
        {
            return; // Skip test
        }

        // Arrange - Make multiple changes to the source
        var originalContent = File.ReadAllText(_originalSourceFile);

        // 1. Add a new property
        var modifiedContent = AddPropertyToClass(
            originalContent,
            "BuildJsonConfig",
            "public int? TestIntProperty { get; set; }"
        );

        // 2. Remove a property
        modifiedContent = RemovePropertyFromClass(
            modifiedContent,
            "BuildJsonConfig",
            "LocalNugetFeedBaseUrl"
        );

        // 3. Change a property type
        modifiedContent = ChangePropertyType(
            modifiedContent,
            "BuildJsonConfig",
            "SyncLocalNugetFeed",
            "bool",
            "string?"
        );

        File.WriteAllText(_tempSourceFile, modifiedContent);

        // Act - Generate schema from modified source
        GenerateSchemaFromSource(_tempSourceFile, _tempSchemaFile);

        // Assert - Verify all changes are reflected
        var schemaContent = File.ReadAllText(_tempSchemaFile);
        using var doc = JsonDocument.Parse(schemaContent);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("properties", out var properties),
            "Schema should have properties definition");

        // Verify added property
        Assert.True(properties.TryGetProperty("testIntProperty", out var testIntProp),
            "Schema should include the newly added property 'testIntProperty'");

        // Verify removed property is gone
        Assert.False(properties.TryGetProperty("localNugetFeedBaseUrl", out _),
            "Schema should not include the removed property 'localNugetFeedBaseUrl'");

        // Verify type change
        Assert.True(properties.TryGetProperty("syncLocalNugetFeed", out var syncProp),
            "Schema should include the 'syncLocalNugetFeed' property");

        // Check that it's now a string type (not boolean)
        var hasStringType = false;
        if (syncProp.TryGetProperty("type", out var typeProperty))
        {
            var typeValue = typeProperty.ValueKind == JsonValueKind.Array
                ? string.Join(",", typeProperty.EnumerateArray().Select(t => t.GetString() ?? ""))
                : typeProperty.GetString();
            hasStringType = typeValue?.Contains("string") ?? false;
        }
        else if (syncProp.TryGetProperty("anyOf", out var anyOf))
        {
            foreach (var option in anyOf.EnumerateArray())
            {
                if (option.TryGetProperty("type", out var optionType) &&
                    optionType.GetString() == "string")
                {
                    hasStringType = true;
                    break;
                }
            }
        }

        Assert.True(hasStringType, "Property type should be changed to string");
    }

    #region Helper Methods

    /// <summary>
    /// Check if npx is available in the system PATH
    /// </summary>
    private bool IsNpxAvailable()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "npx",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Find the repository root by walking up the directory tree looking for .git
    /// </summary>
    private static string FindRepositoryRoot()
    {
        var assemblyLocation = typeof(SchemaSynchronizationTests).Assembly.Location;
        var directory = Path.GetDirectoryName(assemblyLocation);

        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory, ".git")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("Could not find repository root (no .git directory found)");
    }

    /// <summary>
    /// Generate JSON schema from a C# source file using QuickType via npx
    /// </summary>
    private void GenerateSchemaFromSource(string sourceFile, string outputFile)
    {
        var repoRoot = FindRepositoryRoot();

        // Build QuickType command
        var args = $"quicktype --src \"{sourceFile}\" --out \"{outputFile}\" --lang schema --top-level BuildJsonConfig --just-types";

        var startInfo = new ProcessStartInfo
        {
            FileName = "npx",
            Arguments = args,
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            var output = process.StandardOutput.ReadToEnd();
            throw new Exception(
                $"QuickType failed with exit code {process.ExitCode}\n" +
                $"Error: {error}\n" +
                $"Output: {output}"
            );
        }

        Assert.True(File.Exists(outputFile), $"Schema file should be generated at {outputFile}");
    }

    /// <summary>
    /// Add a property to a C# class
    /// </summary>
    private string AddPropertyToClass(string sourceContent, string className, string propertyDeclaration)
    {
        // Find the class and add the property before the closing brace
        var classPattern = new Regex(
            $@"(public\s+sealed\s+class\s+{className}\s*\{{[^}}]*?)(\}})",
            RegexOptions.Singleline
        );

        var match = classPattern.Match(sourceContent);
        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not find class {className} in source");
        }

        // Insert the new property before the closing brace
        var modifiedContent = sourceContent.Substring(0, match.Groups[1].Index + match.Groups[1].Length) +
                             "\n    " + propertyDeclaration + "\n" +
                             sourceContent.Substring(match.Groups[1].Index + match.Groups[1].Length);

        return modifiedContent;
    }

    /// <summary>
    /// Remove a property from a C# class
    /// </summary>
    private string RemovePropertyFromClass(string sourceContent, string className, string propertyName)
    {
        // Find and remove the property declaration (including XML comments and attributes)
        var propertyPattern = new Regex(
            $@"(\s*///[^\n]*\n)*\s*(\[[^\]]*\]\s*)*\s*public\s+[^{{}}]+{propertyName}\s*\{{[^}}]*\}}\s*",
            RegexOptions.Multiline
        );

        var modifiedContent = propertyPattern.Replace(sourceContent, "");

        return modifiedContent;
    }

    /// <summary>
    /// Change the type of a property in a C# class
    /// </summary>
    private string ChangePropertyType(
        string sourceContent,
        string className,
        string propertyName,
        string oldType,
        string newType)
    {
        // Find the property and change its type
        var propertyPattern = new Regex(
            $@"(public\s+){oldType}(\s+{propertyName}\s*\{{)",
            RegexOptions.Multiline
        );

        var modifiedContent = propertyPattern.Replace(
            sourceContent,
            $"${{1}}{newType}${{2}}"
        );

        return modifiedContent;
    }

    #endregion
}
