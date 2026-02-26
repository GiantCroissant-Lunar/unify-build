using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Nuke.Common.IO;
using Serilog;
using UnifyBuild.Nuke.Validation;

namespace UnifyBuild.Nuke.Commands;

/// <summary>
/// Status of a doctor check.
/// </summary>
public enum DoctorStatus
{
    Pass,
    Fail,
    Warning
}

/// <summary>
/// A single doctor check result.
/// </summary>
public sealed record DoctorCheck(
    string Name,
    DoctorStatus Status,
    string Message,
    string? FixSuggestion,
    bool AutoFixable
);

/// <summary>
/// Result of the doctor command execution.
/// </summary>
public sealed record DoctorResult(
    List<DoctorCheck> Checks,
    int FixableCount,
    int FixedCount
);

/// <summary>
/// Runs environment health checks and diagnoses common build configuration issues.
/// Supports auto-fix mode for issues that can be resolved automatically.
/// </summary>
public sealed class DoctorCommand
{
    /// <summary>
    /// Executes all doctor checks against the repository root.
    /// When autoFix is true, attempts to automatically resolve fixable issues.
    /// </summary>
    public DoctorResult Execute(AbsolutePath repoRoot, bool autoFix)
    {
        var checks = new List<DoctorCheck>();
        var fixedCount = 0;

        // 1. dotnet SDK installed and version check
        checks.Add(CheckDotnetSdk());

        // 2. NUKE global tool installed
        checks.Add(CheckNukeInstalled());

        // 3. build.config.json exists
        var configPath = repoRoot / "build.config.json";
        var configExistsCheck = CheckConfigExists(configPath);
        checks.Add(configExistsCheck);

        if (configExistsCheck.Status == DoctorStatus.Fail && autoFix && configExistsCheck.AutoFixable)
        {
            if (TryFixMissingConfig(repoRoot))
            {
                fixedCount++;
                checks[checks.Count - 1] = configExistsCheck with
                {
                    Status = DoctorStatus.Pass,
                    Message = "build.config.json created via init command."
                };
            }
        }

        // Only run config-dependent checks if config exists
        if (File.Exists(configPath))
        {
            // 4. Schema validation
            checks.Add(CheckSchemaValidation(configPath));

            // 5. Semantic validation
            var semanticChecks = CheckSemanticValidation(repoRoot, configPath, autoFix, out var semanticFixed);
            checks.AddRange(semanticChecks);
            fixedCount += semanticFixed;
        }

        // 6. UnifyBuild tool version check
        checks.Add(CheckToolVersion());

        var fixableCount = checks.Count(c => c.AutoFixable && c.Status != DoctorStatus.Pass);

        return new DoctorResult(checks, fixableCount, fixedCount);
    }

    /// <summary>
    /// Checks that the dotnet SDK is installed and reports its version.
    /// </summary>
    internal DoctorCheck CheckDotnetSdk()
    {
        var (exitCode, output) = RunProcess("dotnet", "--version");
        if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
        {
            var version = output.Trim();
            return new DoctorCheck(
                "dotnet SDK",
                DoctorStatus.Pass,
                $"dotnet SDK {version} is installed.",
                null,
                AutoFixable: false
            );
        }

        return new DoctorCheck(
            "dotnet SDK",
            DoctorStatus.Fail,
            "dotnet SDK is not installed or not found in PATH.",
            "Install the .NET SDK from https://dotnet.microsoft.com/download",
            AutoFixable: false
        );
    }

    /// <summary>
    /// Checks that the NUKE global tool is installed.
    /// </summary>
    internal DoctorCheck CheckNukeInstalled()
    {
        var (exitCode, output) = RunProcess("dotnet", "tool list -g");
        if (exitCode == 0 && output.Contains("nuke", StringComparison.OrdinalIgnoreCase))
        {
            return new DoctorCheck(
                "NUKE Global Tool",
                DoctorStatus.Pass,
                "NUKE global tool is installed.",
                null,
                AutoFixable: false
            );
        }

        return new DoctorCheck(
            "NUKE Global Tool",
            DoctorStatus.Warning,
            "NUKE global tool is not installed. It is optional but recommended.",
            "Install via: dotnet tool install Nuke.GlobalTool --global",
            AutoFixable: false
        );
    }

    /// <summary>
    /// Checks that build.config.json exists at the expected path.
    /// </summary>
    internal DoctorCheck CheckConfigExists(AbsolutePath configPath)
    {
        if (File.Exists(configPath))
        {
            return new DoctorCheck(
                "build.config.json",
                DoctorStatus.Pass,
                "build.config.json exists.",
                null,
                AutoFixable: false
            );
        }

        return new DoctorCheck(
            "build.config.json",
            DoctorStatus.Fail,
            $"build.config.json not found at {configPath}.",
            "Run 'dotnet unify-build init' to create a configuration file.",
            AutoFixable: true
        );
    }

    /// <summary>
    /// Validates build.config.json against the JSON Schema.
    /// </summary>
    internal DoctorCheck CheckSchemaValidation(AbsolutePath configPath)
    {
        var validator = new ConfigValidator();
        var result = validator.ValidateSchema(configPath);

        if (result.IsValid)
        {
            var warningCount = result.Warnings.Count();
            var message = warningCount > 0
                ? $"Schema validation passed with {warningCount} warning(s)."
                : "Schema validation passed.";

            return new DoctorCheck(
                "Schema Validation",
                warningCount > 0 ? DoctorStatus.Warning : DoctorStatus.Pass,
                message,
                warningCount > 0 ? "Run 'dotnet unify-build validate' for details." : null,
                AutoFixable: false
            );
        }

        var errorCount = result.Errors.Count();
        return new DoctorCheck(
            "Schema Validation",
            DoctorStatus.Fail,
            $"Schema validation failed with {errorCount} error(s).",
            "Run 'dotnet unify-build validate' to see detailed error messages.",
            AutoFixable: false
        );
    }

    /// <summary>
    /// Runs semantic validation checks: source dirs exist, projects exist, no duplicates.
    /// Returns individual checks for each category. When autoFix is true, creates missing directories.
    /// </summary>
    internal List<DoctorCheck> CheckSemanticValidation(
        AbsolutePath repoRoot,
        AbsolutePath configPath,
        bool autoFix,
        out int fixedCount)
    {
        fixedCount = 0;
        var checks = new List<DoctorCheck>();

        BuildJsonConfig? config = null;
        try
        {
            var json = File.ReadAllText(configPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            config = JsonSerializer.Deserialize<BuildJsonConfig>(json, options);
        }
        catch (Exception ex)
        {
            checks.Add(new DoctorCheck(
                "Config Parsing",
                DoctorStatus.Fail,
                $"Failed to parse build.config.json: {ex.Message}",
                "Fix the JSON syntax errors in build.config.json.",
                AutoFixable: false
            ));
            return checks;
        }

        if (config is null)
        {
            checks.Add(new DoctorCheck(
                "Config Parsing",
                DoctorStatus.Fail,
                "build.config.json deserialized to null.",
                "Ensure build.config.json contains valid JSON content.",
                AutoFixable: false
            ));
            return checks;
        }

        // Source directories check
        var sourceDirCheck = CheckSourceDirectories(repoRoot, config, autoFix, out var dirFixed);
        checks.Add(sourceDirCheck);
        fixedCount += dirFixed;

        // Project references check
        checks.Add(CheckProjectReferences(repoRoot, config));

        // Duplicate projects check
        checks.Add(CheckDuplicateProjects(config));

        return checks;
    }

    /// <summary>
    /// Checks that all source directories referenced in project groups exist.
    /// When autoFix is true, creates missing directories.
    /// </summary>
    internal DoctorCheck CheckSourceDirectories(
        AbsolutePath repoRoot,
        BuildJsonConfig config,
        bool autoFix,
        out int fixedCount)
    {
        fixedCount = 0;

        if (config.ProjectGroups is null || config.ProjectGroups.Count == 0)
        {
            return new DoctorCheck(
                "Source Directories",
                DoctorStatus.Pass,
                "No project groups configured.",
                null,
                AutoFixable: false
            );
        }

        var missingDirs = new List<string>();
        foreach (var (groupName, group) in config.ProjectGroups)
        {
            if (string.IsNullOrWhiteSpace(group.SourceDir))
                continue;

            var sourceDir = repoRoot / group.SourceDir;
            if (!Directory.Exists(sourceDir))
            {
                if (autoFix)
                {
                    try
                    {
                        Directory.CreateDirectory(sourceDir);
                        fixedCount++;
                        Log.Information("Created missing directory: {Dir}", sourceDir);
                    }
                    catch
                    {
                        missingDirs.Add($"{group.SourceDir} (group '{groupName}')");
                    }
                }
                else
                {
                    missingDirs.Add($"{group.SourceDir} (group '{groupName}')");
                }
            }
        }

        if (missingDirs.Count == 0)
        {
            return new DoctorCheck(
                "Source Directories",
                DoctorStatus.Pass,
                "All source directories exist.",
                null,
                AutoFixable: false
            );
        }

        return new DoctorCheck(
            "Source Directories",
            DoctorStatus.Fail,
            $"Missing source directories: {string.Join(", ", missingDirs)}.",
            "Create the missing directories or update sourceDir in build.config.json.",
            AutoFixable: true
        );
    }

    /// <summary>
    /// Checks that all referenced projects can be found in their source directories.
    /// </summary>
    internal DoctorCheck CheckProjectReferences(AbsolutePath repoRoot, BuildJsonConfig config)
    {
        if (config.ProjectGroups is null || config.ProjectGroups.Count == 0)
        {
            return new DoctorCheck(
                "Project References",
                DoctorStatus.Pass,
                "No project groups configured.",
                null,
                AutoFixable: false
            );
        }

        var missingProjects = new List<string>();
        foreach (var (groupName, group) in config.ProjectGroups)
        {
            if (group.Include is null || group.Include.Length == 0)
                continue;

            var sourceDir = repoRoot / group.SourceDir;
            if (!Directory.Exists(sourceDir))
                continue; // Already reported by source dir check

            var existingProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var file in Directory.GetFiles(sourceDir, "*.csproj", SearchOption.AllDirectories))
                {
                    existingProjects.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            catch
            {
                continue;
            }

            foreach (var projectName in group.Include)
            {
                if (!existingProjects.Contains(projectName))
                {
                    missingProjects.Add($"{projectName} (group '{groupName}')");
                }
            }
        }

        if (missingProjects.Count == 0)
        {
            return new DoctorCheck(
                "Project References",
                DoctorStatus.Pass,
                "All referenced projects exist.",
                null,
                AutoFixable: false
            );
        }

        return new DoctorCheck(
            "Project References",
            DoctorStatus.Fail,
            $"Missing projects: {string.Join(", ", missingProjects)}.",
            "Verify project names in 'include' lists match existing .csproj files.",
            AutoFixable: false
        );
    }

    /// <summary>
    /// Checks for duplicate project references across project groups.
    /// </summary>
    internal DoctorCheck CheckDuplicateProjects(BuildJsonConfig config)
    {
        if (config.ProjectGroups is null || config.ProjectGroups.Count == 0)
        {
            return new DoctorCheck(
                "Duplicate Projects",
                DoctorStatus.Pass,
                "No project groups configured.",
                null,
                AutoFixable: false
            );
        }

        var projectToGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var duplicatesWithinGroup = new List<string>();

        foreach (var (groupName, group) in config.ProjectGroups)
        {
            if (group.Include is null || group.Include.Length == 0)
                continue;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var projectName in group.Include)
            {
                if (!seen.Add(projectName))
                {
                    duplicatesWithinGroup.Add($"{projectName} in group '{groupName}'");
                }

                if (!projectToGroups.TryGetValue(projectName, out var groups))
                {
                    groups = new List<string>();
                    projectToGroups[projectName] = groups;
                }
                groups.Add(groupName);
            }
        }

        var crossGroupDuplicates = projectToGroups
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => $"{kvp.Key} in groups [{string.Join(", ", kvp.Value)}]")
            .ToList();

        var allDuplicates = duplicatesWithinGroup.Concat(crossGroupDuplicates).ToList();

        if (allDuplicates.Count == 0)
        {
            return new DoctorCheck(
                "Duplicate Projects",
                DoctorStatus.Pass,
                "No duplicate project references found.",
                null,
                AutoFixable: false
            );
        }

        return new DoctorCheck(
            "Duplicate Projects",
            DoctorStatus.Fail,
            $"Duplicate project references: {string.Join("; ", allDuplicates)}.",
            "Remove duplicate entries from project group 'include' lists.",
            AutoFixable: false
        );
    }

    /// <summary>
    /// Checks the UnifyBuild tool version.
    /// </summary>
    internal DoctorCheck CheckToolVersion()
    {
        var assembly = typeof(DoctorCommand).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString()
                      ?? "unknown";

        return new DoctorCheck(
            "UnifyBuild Version",
            DoctorStatus.Pass,
            $"UnifyBuild.Nuke version: {version}.",
            null,
            AutoFixable: false
        );
    }

    /// <summary>
    /// Attempts to fix a missing build.config.json by running InitCommand.
    /// </summary>
    private static bool TryFixMissingConfig(AbsolutePath repoRoot)
    {
        try
        {
            var initCommand = new InitCommand();
            var options = new InitOptions(
                OutputPath: repoRoot,
                Interactive: false,
                Template: null,
                Force: false
            );
            initCommand.Execute(repoRoot, options);
            Log.Information("Auto-fix: Created build.config.json via init command.");
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning("Auto-fix failed: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Runs an external process and captures its output.
    /// </summary>
    internal static (int ExitCode, string Output) RunProcess(string fileName, string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(15000);

            return (process.ExitCode, string.IsNullOrWhiteSpace(output) ? error : output);
        }
        catch
        {
            return (-1, string.Empty);
        }
    }
}
