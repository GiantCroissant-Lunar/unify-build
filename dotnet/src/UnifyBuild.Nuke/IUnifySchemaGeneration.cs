using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using static Nuke.Common.Tools.Npm.NpmTasks;

namespace UnifyBuild.Nuke;

/// <summary>
/// Schema generation targets: Automated JSON schema generation from C# configuration classes.
/// </summary>
public interface IUnifySchemaGeneration : IUnifyBuildConfig
{
    /// <summary>
    /// Install QuickType as a local npm tool if not already present.
    /// This target is idempotent - it skips installation if QuickType is already available.
    /// </summary>
    Target InstallQuickTypeTool => _ => _
        .Description("Install QuickType as a local npm tool")
        .OnlyWhenDynamic(() => !IsQuickTypeInstalled())
        .Executes(() =>
        {
            var configDir = RootDirectory / ".config";
            
            try
            {
                Serilog.Log.Information("Installing QuickType via npm in {ConfigDir}...", configDir);
                
                // Run npm install in the .config directory
                var result = ProcessTasks.StartProcess(
                    "npm",
                    "install",
                    configDir
                ).AssertWaitForExit();
                
                if (result.ExitCode != 0)
                {
                    throw new Exception($"npm install failed with exit code {result.ExitCode}");
                }
                
                Serilog.Log.Information("QuickType installed successfully");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to install QuickType");
                Serilog.Log.Error(
                    "QuickType installation failed. This may be due to network issues or npm configuration problems.\n" +
                    "\n" +
                    "MANUAL INSTALLATION INSTRUCTIONS:\n" +
                    "  1. Navigate to the .config directory: cd {ConfigDir}\n" +
                    "  2. Run: npm install\n" +
                    "  3. Verify installation: npx quicktype --version\n" +
                    "\n" +
                    "TROUBLESHOOTING:\n" +
                    "  • Check your internet connection\n" +
                    "  • Verify npm is installed and accessible (run 'npm --version')\n" +
                    "  • Check if a proxy or firewall is blocking npm registry access\n" +
                    "  • Try clearing npm cache: npm cache clean --force\n" +
                    "  • Check npm registry configuration: npm config get registry\n" +
                    "  • If behind a corporate proxy, configure npm proxy settings:\n" +
                    "    npm config set proxy http://proxy.company.com:8080\n" +
                    "    npm config set https-proxy http://proxy.company.com:8080",
                    configDir
                );
                throw;
            }
        });

    /// <summary>
    /// Check if QuickType is already installed by verifying the node_modules directory exists
    /// and contains the quicktype package.
    /// </summary>
    private bool IsQuickTypeInstalled()
    {
        var configDir = RootDirectory / ".config";
        var nodeModulesDir = configDir / "node_modules";
        var quicktypeDir = nodeModulesDir / "quicktype";
        
        // Check if node_modules/quicktype exists
        if (!Directory.Exists(quicktypeDir))
        {
            Serilog.Log.Debug("QuickType not found at {QuickTypeDir}", quicktypeDir);
            return false;
        }
        
        // Verify the quicktype executable exists
        var quicktypeBin = quicktypeDir / "dist" / "index.js";
        if (!File.Exists(quicktypeBin))
        {
            Serilog.Log.Debug("QuickType binary not found at {QuickTypeBin}", quicktypeBin);
            return false;
        }
        
        Serilog.Log.Debug("QuickType is already installed");
        return true;
    }

    /// <summary>
    /// Extract file path and line number information from QuickType error output.
    /// QuickType error messages may contain patterns like "file.cs:line:column" or "at line X".
    /// </summary>
    /// <param name="errorOutput">The error output from QuickType</param>
    /// <returns>Extracted line information, or empty string if not found</returns>
    private string ExtractLineInfoFromError(string errorOutput)
    {
        if (string.IsNullOrEmpty(errorOutput))
        {
            return string.Empty;
        }

        // Try to match patterns like "file.cs:123:45" or "file.cs:123"
        var fileLinePattern = new System.Text.RegularExpressions.Regex(
            @"([^\s]+\.cs):(\d+)(?::(\d+))?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
        var fileLineMatch = fileLinePattern.Match(errorOutput);
        if (fileLineMatch.Success)
        {
            var file = fileLineMatch.Groups[1].Value;
            var line = fileLineMatch.Groups[2].Value;
            var column = fileLineMatch.Groups[3].Success ? fileLineMatch.Groups[3].Value : null;
            
            return column != null 
                ? $"{file} at line {line}, column {column}" 
                : $"{file} at line {line}";
        }

        // Try to match patterns like "at line 123" or "line 123"
        var linePattern = new System.Text.RegularExpressions.Regex(
            @"(?:at\s+)?line\s+(\d+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
        var lineMatch = linePattern.Match(errorOutput);
        if (lineMatch.Success)
        {
            return $"line {lineMatch.Groups[1].Value}";
        }

        return string.Empty;
    }

    /// <summary>
    /// Generate JSON schema from a C# source file using QuickType.
    /// </summary>
    /// <param name="sourceFile">Path to the C# source file (must exist and end with .cs)</param>
    /// <param name="outputFile">Path where the JSON schema will be written</param>
    /// <param name="topLevelType">Name of the top-level C# type to generate schema for (default: "BuildJsonConfig")</param>
    /// <exception cref="ArgumentException">Thrown when sourceFile doesn't exist or isn't a .cs file</exception>
    /// <exception cref="Exception">Thrown when QuickType execution fails</exception>
    private void GenerateSchemaFromCSharp(
        AbsolutePath sourceFile,
        AbsolutePath outputFile,
        string topLevelType = "BuildJsonConfig")
    {
        // Validate input file exists
        if (!File.Exists(sourceFile))
        {
            throw new ArgumentException($"Source file does not exist: {sourceFile}", nameof(sourceFile));
        }

        // Validate input file is a C# file
        if (!sourceFile.ToString().EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Source file must be a .cs file: {sourceFile}", nameof(sourceFile));
        }

        // Create output directory if it doesn't exist
        var outputDir = outputFile.Parent;
        if (!Directory.Exists(outputDir))
        {
            try
            {
                Serilog.Log.Information("Creating output directory: {OutputDir}", outputDir);
                Directory.CreateDirectory(outputDir);
            }
            catch (UnauthorizedAccessException ex)
            {
                Serilog.Log.Error(
                    ex,
                    "Permission denied when creating output directory.\n" +
                    "Directory path: {OutputDir}\n" +
                    "The build process does not have permission to create this directory.\n" +
                    "To resolve this issue:\n" +
                    "  1. Check directory permissions on the parent path\n" +
                    "  2. Ensure you have write access to the artifacts directory\n" +
                    "  3. Verify the path is not restricted by system policies\n" +
                    "  4. Try running the build with elevated permissions if necessary",
                    outputDir
                );
                throw;
            }
            catch (IOException ex)
            {
                Serilog.Log.Error(
                    ex,
                    "I/O error when creating output directory.\n" +
                    "Directory path: {OutputDir}\n" +
                    "Error details: {Message}\n" +
                    "Possible causes:\n" +
                    "  • Path is too long (exceeds system limits)\n" +
                    "  • Invalid characters in path\n" +
                    "  • Network drive is unavailable (if path is on network)\n" +
                    "  • Disk is full or out of space",
                    outputDir, ex.Message
                );
                throw;
            }
        }

        // Build QuickType command arguments
        var args = string.Join(" ",
            "--src", $"\"{sourceFile}\"",
            "--out", $"\"{outputFile}\"",
            "--lang", "schema",
            "--top-level", topLevelType,
            "--just-types"
        );

        Serilog.Log.Information("Generating JSON schema from {SourceFile}...", sourceFile);
        Serilog.Log.Debug("QuickType arguments: {Args}", args);

        try
        {
            // Execute QuickType via npx
            var result = ProcessTasks.StartProcess(
                "npx",
                $"quicktype {args}",
                RootDirectory
            ).AssertWaitForExit();

            if (result.ExitCode != 0)
            {
                var errorOutput = string.Join(Environment.NewLine, result.Output.Select(o => o.Text));
                
                // Extract file path and line number from QuickType error output if present
                // QuickType error format typically includes patterns like "file.cs:line:column" or "at line X"
                var lineInfo = ExtractLineInfoFromError(errorOutput);
                var lineInfoMessage = !string.IsNullOrEmpty(lineInfo) 
                    ? $"\nError location: {lineInfo}" 
                    : "";
                
                Serilog.Log.Error(
                    "QuickType failed to parse C# source file (exit code {ExitCode}).\n" +
                    "Source file: {SourceFile}{LineInfo}\n" +
                    "Error output:\n{ErrorOutput}",
                    result.ExitCode, sourceFile, lineInfoMessage, errorOutput
                );
                
                throw new Exception(
                    $"QuickType failed to generate schema (exit code {result.ExitCode}).\n" +
                    $"Source file: {sourceFile}{lineInfoMessage}\n" +
                    $"Error output:\n{errorOutput}"
                );
            }

            // Validate output file was created
            if (!File.Exists(outputFile))
            {
                throw new Exception(
                    $"QuickType completed but output file was not created: {outputFile}\n" +
                    $"This may indicate a QuickType bug or configuration issue."
                );
            }

            Serilog.Log.Information("Successfully generated schema: {OutputFile}", outputFile);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Handle file permission errors specifically (Requirement 12.4)
            Serilog.Log.Error(
                ex,
                "Permission denied when writing schema file.\n" +
                "Output path: {OutputFile}\n" +
                "The build process does not have permission to write to this location.\n" +
                "To resolve this issue:\n" +
                "  1. Check file permissions on the output directory: {OutputDir}\n" +
                "  2. Ensure the directory is not read-only\n" +
                "  3. Verify you have write access to the artifacts directory\n" +
                "  4. On Windows, check if the file is locked by another process\n" +
                "  5. Try running the build with elevated permissions if necessary",
                outputFile, outputFile.Parent
            );
            throw;
        }
        catch (IOException ex)
        {
            // Handle other I/O errors (disk full, path too long, etc.) (Requirement 12.4)
            Serilog.Log.Error(
                ex,
                "File I/O error when writing schema file.\n" +
                "Output path: {OutputFile}\n" +
                "Error details: {Message}\n" +
                "Possible causes:\n" +
                "  • Disk is full or out of space\n" +
                "  • Path is too long (exceeds system limits)\n" +
                "  • File is locked by another process\n" +
                "  • Network drive is unavailable (if output is on network path)\n" +
                "  • Antivirus software is blocking file access",
                outputFile, ex.Message
            );
            throw;
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            Serilog.Log.Error(ex, "Failed to generate JSON schema");
            Serilog.Log.Error(
                "Schema generation failed. Troubleshooting steps:\n" +
                "  1. Verify the C# source file has valid syntax: {SourceFile}\n" +
                "  2. Ensure QuickType is installed (run InstallQuickTypeTool target)\n" +
                "  3. Check that the top-level type '{TopLevelType}' exists in the source file\n" +
                "  4. Verify npx is available in PATH (run 'npx --version')\n" +
                "  5. Try running QuickType manually: npx quicktype {Args}",
                sourceFile, topLevelType, args
            );
            throw;
        }
    }

    /// <summary>
    /// Validate that a generated schema file is valid JSON Schema format.
    /// </summary>
    /// <param name="schemaFile">Path to the schema file to validate</param>
    /// <returns>True if the schema is valid, false otherwise</returns>
    /// <exception cref="ArgumentException">Thrown when schemaFile doesn't exist</exception>
    private bool ValidateGeneratedSchema(AbsolutePath schemaFile)
    {
        // Validate schema file exists
        if (!File.Exists(schemaFile))
        {
            throw new ArgumentException($"Schema file does not exist: {schemaFile}", nameof(schemaFile));
        }

        Serilog.Log.Information("Validating generated schema: {SchemaFile}", schemaFile);

        try
        {
            // Read and parse schema content as JSON
            var schemaContent = File.ReadAllText(schemaFile);
            JsonDocument schema;
            
            try
            {
                schema = JsonDocument.Parse(schemaContent);
            }
            catch (JsonException ex)
            {
                Serilog.Log.Error(ex, "Invalid JSON in schema file: {SchemaFile}", schemaFile);
                Serilog.Log.Error("The schema file contains malformed JSON. Details: {Message}", ex.Message);
                return false;
            }

            var root = schema.RootElement;

            // Check for $schema property (warning only, not required)
            if (!root.TryGetProperty("$schema", out _))
            {
                Serilog.Log.Warning("Schema file is missing $schema property. This is recommended but not required.");
            }

            // Validate root type is "object" (required)
            if (!root.TryGetProperty("type", out var typeProperty))
            {
                Serilog.Log.Error("Schema validation failed: Missing 'type' property at root level");
                return false;
            }

            var typeValue = typeProperty.GetString();
            if (typeValue != "object")
            {
                Serilog.Log.Error(
                    "Schema validation failed: Root type must be 'object', but found '{TypeValue}'",
                    typeValue
                );
                return false;
            }

            // Validate properties definition exists (required)
            if (!root.TryGetProperty("properties", out _))
            {
                Serilog.Log.Error("Schema validation failed: Missing 'properties' definition at root level");
                return false;
            }

            Serilog.Log.Information("Schema validation passed: {SchemaFile}", schemaFile);
            return true;
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            Serilog.Log.Error(ex, "Unexpected error during schema validation");
            throw;
        }
    }

    /// <summary>
    /// Generate JSON schema from BuildConfigJson.cs and validate the output.
    /// This target runs before Pack to ensure the schema is always up-to-date in the package.
    /// </summary>
    Target GenerateSchema => _ => _
        .Description("Generate JSON schema from BuildConfigJson.cs")
        .DependsOn(InstallQuickTypeTool)
        .Before<IUnifyPack>(x => x.Pack)
        .Executes(() =>
        {
            try
            {
                // Define paths
                var sourceFile = RootDirectory / "dotnet" / "src" / "UnifyBuild.Nuke" / "BuildConfigJson.cs";
                var artifactsRoot = RootDirectory / "build" / "_artifacts";
                var version = UnifyConfig.ArtifactsVersion ?? UnifyConfig.Version ?? "local";
                var outputFile = artifactsRoot / version / "build.config.schema.json";

                Serilog.Log.Information("Starting schema generation...");
                Serilog.Log.Information("  Source: {SourceFile}", sourceFile);
                Serilog.Log.Information("  Output: {OutputFile}", outputFile);

                // Generate schema from C# source
                GenerateSchemaFromCSharp(sourceFile, outputFile, "BuildJsonConfig");

                // Validate the generated schema
                if (!ValidateGeneratedSchema(outputFile))
                {
                    throw new Exception(
                        $"Schema validation failed for {outputFile}. " +
                        "The generated schema does not meet JSON Schema requirements. " +
                        "See error messages above for details."
                    );
                }

                Serilog.Log.Information("✓ Schema generation completed successfully");
                Serilog.Log.Information("  Schema file: {OutputFile}", outputFile);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Schema generation failed");
                Serilog.Log.Error(
                    "The GenerateSchema target failed. This will prevent the Pack target from executing.\n" +
                    "Common causes:\n" +
                    "  • QuickType is not installed (run InstallQuickTypeTool target)\n" +
                    "  • BuildConfigJson.cs has syntax errors\n" +
                    "  • Network issues preventing npm package installation\n" +
                    "  • File permission issues in the output directory\n" +
                    "\n" +
                    "To troubleshoot:\n" +
                    "  1. Check the error messages above for specific details\n" +
                    "  2. Verify BuildConfigJson.cs compiles without errors\n" +
                    "  3. Ensure npm and npx are available in PATH\n" +
                    "  4. Try running 'npm install' in the .config directory manually"
                );
                throw;
            }
        });
}
