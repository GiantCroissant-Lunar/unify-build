using NJsonSchema;
using Nuke.Common.IO;
using UnifyBuild.Nuke.Diagnostics;

namespace UnifyBuild.Nuke.Validation;

/// <summary>
/// Validates build configuration files against JSON Schema and performs semantic checks
/// such as verifying project references exist, source directories exist, and no duplicate projects.
/// </summary>
public sealed class ConfigValidator
{
    private readonly string? _schemaPath;

    /// <summary>
    /// Creates a new ConfigValidator with an optional explicit schema file path.
    /// If not provided, the validator will search for build.config.schema.json in common locations.
    /// </summary>
    public ConfigValidator(string? schemaPath = null)
    {
        _schemaPath = schemaPath;
    }

    /// <summary>
    /// Validates a build config JSON file against the JSON Schema.
    /// Returns accumulated issues with severity, code, message, file path, line, and suggestion.
    /// </summary>
    public ValidationResult ValidateSchema(string configPath)
    {
        var issues = new List<ValidationIssue>();

        if (!File.Exists(configPath))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                FormatCode(ErrorCode.ConfigNotFound),
                $"Configuration file not found: {configPath}",
                configPath,
                Suggestion: "Run 'dotnet unify-build init' to create a configuration file."
            ));
            return new ValidationResult(false, issues);
        }

        string json;
        try
        {
            json = File.ReadAllText(configPath);
        }
        catch (Exception ex)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                FormatCode(ErrorCode.ConfigParseError),
                $"Failed to read configuration file: {ex.Message}",
                configPath
            ));
            return new ValidationResult(false, issues);
        }

        var schemaPath = ResolveSchemaPath(configPath);
        if (schemaPath is null)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Warning,
                FormatCode(ErrorCode.SchemaValidationFailed),
                "JSON Schema file not found. Schema validation skipped.",
                Suggestion: "Ensure build.config.schema.json exists in the repository or artifacts directory."
            ));
            return new ValidationResult(true, issues);
        }

        string schemaJson;
        try
        {
            schemaJson = File.ReadAllText(schemaPath);
        }
        catch (Exception ex)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Warning,
                FormatCode(ErrorCode.SchemaValidationFailed),
                $"Failed to read schema file: {ex.Message}",
                schemaPath,
                Suggestion: "Verify the schema file is accessible and not corrupted."
            ));
            return new ValidationResult(true, issues);
        }

        try
        {
            var schema = JsonSchema.FromJsonAsync(schemaJson).GetAwaiter().GetResult();
            var errors = schema.Validate(json);

            foreach (var error in errors)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    FormatCode(ErrorCode.ConfigSchemaViolation),
                    $"Schema violation at '{error.Path}': {error.Kind}",
                    configPath,
                    Line: error.LineNumber,
                    Suggestion: $"Check the property at '{error.Path}' matches the expected schema type."
                ));
            }
        }
        catch (Exception ex)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                FormatCode(ErrorCode.SchemaValidationFailed),
                $"Schema validation failed: {ex.Message}",
                configPath
            ));
        }

        return new ValidationResult(!issues.Any(i => i.Severity == ValidationSeverity.Error), issues);
    }

    /// <summary>
    /// Performs semantic validation on a parsed build configuration.
    /// Checks that source directories exist, referenced projects exist, and no duplicate project names.
    /// </summary>
    public ValidationResult ValidateSemantic(AbsolutePath repoRoot, BuildJsonConfig config)
    {
        var issues = new List<ValidationIssue>();

        ValidateSourceDirectories(repoRoot, config, issues);
        ValidateProjectReferences(repoRoot, config, issues);
        ValidateDuplicateProjects(config, issues);
        ValidateUnityBuild(repoRoot, config, issues);

        return new ValidationResult(!issues.Any(i => i.Severity == ValidationSeverity.Error), issues);
    }

    private void ValidateSourceDirectories(AbsolutePath repoRoot, BuildJsonConfig config, List<ValidationIssue> issues)
    {
        if (config.ProjectGroups is null)
            return;

        foreach (var (groupName, group) in config.ProjectGroups)
        {
            if (string.IsNullOrWhiteSpace(group.SourceDir))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    FormatCode(ErrorCode.ConfigDirNotFound),
                    $"Project group '{groupName}' has an empty source directory.",
                    Suggestion: $"Set 'sourceDir' for project group '{groupName}'."
                ));
                continue;
            }

            var sourceDir = repoRoot / group.SourceDir;
            if (!Directory.Exists(sourceDir))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    FormatCode(ErrorCode.ConfigDirNotFound),
                    $"Source directory '{group.SourceDir}' for project group '{groupName}' does not exist. Resolved path: {sourceDir}",
                    sourceDir,
                    Suggestion: $"Create the directory '{group.SourceDir}' or update the 'sourceDir' in project group '{groupName}'."
                ));
            }
        }
    }

    private void ValidateProjectReferences(AbsolutePath repoRoot, BuildJsonConfig config, List<ValidationIssue> issues)
    {
        if (config.ProjectGroups is null)
            return;

        foreach (var (groupName, group) in config.ProjectGroups)
        {
            if (group.Include is null || group.Include.Length == 0)
                continue;

            var sourceDir = repoRoot / group.SourceDir;
            if (!Directory.Exists(sourceDir))
                continue; // Already reported by ValidateSourceDirectories

            var existingProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var csprojFiles = Directory.GetFiles(sourceDir, "*.csproj", SearchOption.AllDirectories);
                foreach (var file in csprojFiles)
                {
                    existingProjects.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            catch
            {
                // If we can't enumerate, skip this check
                continue;
            }

            foreach (var projectName in group.Include)
            {
                if (!existingProjects.Contains(projectName))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        FormatCode(ErrorCode.ConfigProjectNotFound),
                        $"Project '{projectName}' referenced in group '{groupName}' was not found in '{group.SourceDir}'.",
                        Suggestion: $"Verify the project name '{projectName}' exists as a .csproj in '{group.SourceDir}', or remove it from the 'include' list."
                    ));
                }
            }
        }

        // Validate explicit project lists
        ValidateExplicitProjectList(repoRoot, config.CompileProjects, "compileProjects", issues);
        ValidateExplicitProjectList(repoRoot, config.PublishProjects, "publishProjects", issues);
        ValidateExplicitProjectList(repoRoot, config.PackProjects, "packProjects", issues);
    }

    private void ValidateExplicitProjectList(AbsolutePath repoRoot, string[]? projects, string listName, List<ValidationIssue> issues)
    {
        if (projects is null)
            return;

        foreach (var projectPath in projects)
        {
            var fullPath = repoRoot / projectPath;
            if (!File.Exists(fullPath))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    FormatCode(ErrorCode.ConfigProjectNotFound),
                    $"Project '{projectPath}' in '{listName}' does not exist. Resolved path: {fullPath}",
                    Suggestion: $"Verify the project path '{projectPath}' is correct, or remove it from '{listName}'."
                ));
            }
        }
    }

    private void ValidateDuplicateProjects(BuildJsonConfig config, List<ValidationIssue> issues)
    {
        if (config.ProjectGroups is null)
            return;

        // Track project names across all groups: projectName -> list of group names
        var projectToGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (groupName, group) in config.ProjectGroups)
        {
            if (group.Include is null || group.Include.Length == 0)
                continue;

            // Check for duplicates within the same group
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var projectName in group.Include)
            {
                if (!seen.Add(projectName))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        FormatCode(ErrorCode.ConfigDuplicateProject),
                        $"Project '{projectName}' is listed multiple times in group '{groupName}'.",
                        Suggestion: $"Remove the duplicate entry for '{projectName}' in group '{groupName}'."
                    ));
                }

                // Track across groups
                if (!projectToGroups.TryGetValue(projectName, out var groups))
                {
                    groups = new List<string>();
                    projectToGroups[projectName] = groups;
                }
                groups.Add(groupName);
            }
        }

        // Report projects appearing in multiple groups
        foreach (var (projectName, groups) in projectToGroups)
        {
            if (groups.Count > 1)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    FormatCode(ErrorCode.ConfigDuplicateProject),
                    $"Project '{projectName}' appears in multiple groups: {string.Join(", ", groups)}.",
                    Suggestion: $"Remove '{projectName}' from all but one group, or use different project names."
                ));
            }
        }
    }

    private void ValidateUnityBuild(AbsolutePath repoRoot, BuildJsonConfig config, List<ValidationIssue> issues)
    {
        if (config.UnityBuild is null)
            return;

        var unityConfig = config.UnityBuild;

        // Validate Unity project root exists
        if (string.IsNullOrWhiteSpace(unityConfig.UnityProjectRoot))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                FormatCode(ErrorCode.ConfigDirNotFound),
                "Unity build is configured but 'unityProjectRoot' is empty.",
                Suggestion: "Set 'unityProjectRoot' to the path of your Unity project (the folder containing 'Assets/')."
            ));
        }
        else
        {
            var unityRoot = repoRoot / unityConfig.UnityProjectRoot;
            if (!Directory.Exists(unityRoot))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    FormatCode(ErrorCode.ConfigDirNotFound),
                    $"Unity project root '{unityConfig.UnityProjectRoot}' does not exist. Resolved path: {unityRoot}",
                    Suggestion: $"Verify the path '{unityConfig.UnityProjectRoot}' is correct and the Unity project directory exists."
                ));
            }
            else if (!Directory.Exists(unityRoot / "Assets"))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    FormatCode(ErrorCode.ConfigDirNotFound),
                    $"Unity project root '{unityConfig.UnityProjectRoot}' does not contain an 'Assets' directory. This may not be a valid Unity project.",
                    Suggestion: "Ensure 'unityProjectRoot' points to a directory containing 'Assets/' and 'ProjectSettings/'."
                ));
            }
        }

        // Validate target framework compatibility
        var framework = unityConfig.TargetFramework ?? "netstandard2.1";
        var compatibleFrameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "netstandard2.0",
            "netstandard2.1"
        };

        if (!compatibleFrameworks.Contains(framework))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Warning,
                FormatCode(ErrorCode.ConfigSchemaViolation),
                $"Target framework '{framework}' may not be compatible with Unity. Unity supports 'netstandard2.0' and 'netstandard2.1'.",
                Suggestion: "Set 'targetFramework' to 'netstandard2.1' (recommended) or 'netstandard2.0' for Unity compatibility."
            ));
        }

        // Validate package mappings
        if (unityConfig.Packages is null || unityConfig.Packages.Length == 0)
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Warning,
                FormatCode(ErrorCode.ConfigSchemaViolation),
                "Unity build is configured but no package mappings are defined.",
                Suggestion: "Add at least one entry to the 'packages' array with 'packageName' and 'sourceProjects' or 'sourceProjectGlobs'."
            ));
            return;
        }

        foreach (var package in unityConfig.Packages)
        {
            if (string.IsNullOrWhiteSpace(package.PackageName))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    FormatCode(ErrorCode.ConfigSchemaViolation),
                    "A Unity package mapping has an empty 'packageName'.",
                    Suggestion: "Set 'packageName' to the Unity package identifier (e.g., 'com.company.package')."
                ));
            }

            // Validate source projects exist
            if (package.SourceProjects is not null)
            {
                foreach (var projectPath in package.SourceProjects)
                {
                    var fullPath = repoRoot / projectPath;
                    if (!File.Exists(fullPath))
                    {
                        issues.Add(new ValidationIssue(
                            ValidationSeverity.Error,
                            FormatCode(ErrorCode.ConfigProjectNotFound),
                            $"Source project '{projectPath}' in Unity package '{package.PackageName}' does not exist. Resolved path: {fullPath}",
                            Suggestion: $"Verify the project path '{projectPath}' is correct, or remove it from 'sourceProjects'."
                        ));
                    }
                }
            }

            // Warn if no source projects or globs are defined
            var hasSourceProjects = package.SourceProjects is not null && package.SourceProjects.Length > 0;
            var hasSourceGlobs = package.SourceProjectGlobs is not null && package.SourceProjectGlobs.Length > 0;
            if (!hasSourceProjects && !hasSourceGlobs)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    FormatCode(ErrorCode.ConfigSchemaViolation),
                    $"Unity package '{package.PackageName}' has no 'sourceProjects' or 'sourceProjectGlobs' defined.",
                    Suggestion: "Add 'sourceProjects' with explicit .csproj paths or 'sourceProjectGlobs' with glob patterns."
                ));
            }
        }
    }

    private string? ResolveSchemaPath(string configPath)
    {
        // Use explicit schema path if provided
        if (_schemaPath is not null && File.Exists(_schemaPath))
            return _schemaPath;

        // Search relative to config file
        var configDir = Path.GetDirectoryName(configPath);
        if (configDir is not null)
        {
            var schemaInConfigDir = Path.Combine(configDir, "build.config.schema.json");
            if (File.Exists(schemaInConfigDir))
                return schemaInConfigDir;
        }

        // Search upward from config directory for the schema file
        var dir = configDir;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "build.config.schema.json");
            if (File.Exists(candidate))
                return candidate;

            // Also check build/_artifacts subdirectories
            var artifactsDir = Path.Combine(dir, "build", "_artifacts");
            if (Directory.Exists(artifactsDir))
            {
                try
                {
                    var schemaFiles = Directory.GetFiles(artifactsDir, "build.config.schema.json", SearchOption.AllDirectories);
                    if (schemaFiles.Length > 0)
                        return schemaFiles[0];
                }
                catch
                {
                    // Ignore search errors
                }
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    private static string FormatCode(ErrorCode code) => $"UB{(int)code}";
}
