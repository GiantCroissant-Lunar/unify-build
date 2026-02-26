using System;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;
using UnifyBuild.Nuke;
using UnifyBuild.Nuke.Commands;

namespace UnifyBuild.Tool;

/// <summary>
/// Entry point for the unify-build CLI tool.
/// Composes all UnifyBuild components into a single NukeBuild class.
/// </summary>
class Build : NukeBuild, IUnify, IUnifyNative, IUnifyUnity
{
    BuildContext IUnifyBuildConfig.UnifyConfig =>
        BuildContextLoader.FromJson(RootDirectory, "build.config.json");

    [Parameter("Run init in interactive mode with step-by-step prompts")]
    readonly bool Interactive;

    [Parameter("Alias for --interactive: launch the configuration wizard")]
    readonly bool Wizard;

    [Parameter("Template to use for init: 'library' or 'application'")]
    readonly string? Template;

    [Parameter("Force overwrite of existing build.config.json")]
    readonly bool Force;

    /// <summary>
    /// Scaffolds a new build.config.json by discovering projects and generating configuration.
    /// Supports --interactive, --template, and --force parameters.
    /// </summary>
    Target Init => _ => _
        .Description("Initialize a new build.config.json configuration file")
        .Executes(() =>
        {
            var command = new InitCommand();
            var options = new InitOptions(
                OutputPath: RootDirectory / "build.config.json",
                Interactive: Interactive || Wizard,
                Template: Template,
                Force: Force
            );

            var result = command.Execute(RootDirectory, options);

            Log.Information("Created configuration at {ConfigPath}", result.ConfigPath);
            Log.Information("Discovered {Count} project(s)", result.DiscoveredProjects.Count);

            foreach (var project in result.DiscoveredProjects)
            {
                Log.Debug("  - {Project}", project);
            }
        });

    /// <summary>
    /// Migrates build.config.json from v1 to v2 schema with automatic backup.
    /// </summary>
    Target Migrate => _ => _
        .Description("Migrate build.config.json from v1 to v2 schema")
        .Executes(() =>
        {
            var command = new MigrateCommand();
            var result = command.Execute(RootDirectory / "build.config.json");

            if (result.Changes.Count > 0)
            {
                Log.Information("Migration applied {Count} change(s):", result.Changes.Count);
                foreach (var change in result.Changes)
                    Log.Information("  {Change}", change);
            }
            else
            {
                Log.Information("No migration changes needed");
            }

            if (!string.IsNullOrEmpty(result.BackupPath))
                Log.Information("Backup saved to {BackupPath}", result.BackupPath);

            Log.Information("Migration {Status}", result.IsValid ? "succeeded" : "completed with validation warnings");
        });

    /// <summary>
    /// Validates build.config.json against schema and checks semantic correctness.
    /// </summary>
    Target Validate => _ => _
        .Description("Validate build.config.json against schema and check semantic correctness")
        .Executes(() =>
        {
            var command = new ValidateCommand();
            var configPath = RootDirectory / "build.config.json";
            var result = command.Execute(configPath, RootDirectory);

            if (!result.IsValid)
            {
                throw new Exception(
                    $"Validation failed with {result.ErrorCount} error(s). Fix the issues above and try again.");
            }
        });

    [Parameter("Auto-fix issues found by doctor")]
    readonly bool Fix;

    /// <summary>
    /// Checks build environment health and diagnoses common issues.
    /// Use --fix to automatically resolve fixable issues.
    /// </summary>
    Target Doctor => _ => _
        .Description("Check build environment health and diagnose common issues")
        .Executes(() =>
        {
            var command = new DoctorCommand();
            var result = command.Execute(RootDirectory, Fix);

            foreach (var check in result.Checks)
            {
                var icon = check.Status switch
                {
                    DoctorStatus.Pass => "✓",
                    DoctorStatus.Warning => "⚠",
                    DoctorStatus.Fail => "✗",
                    _ => "?"
                };

                var message = $"{icon} {check.Name}: {check.Message}";

                switch (check.Status)
                {
                    case DoctorStatus.Pass:
                        Log.Information(message);
                        break;
                    case DoctorStatus.Warning:
                        Log.Warning(message);
                        break;
                    case DoctorStatus.Fail:
                        Log.Error(message);
                        break;
                }

                if (check.FixSuggestion is not null && check.Status != DoctorStatus.Pass)
                    Log.Information("  → {Suggestion}", check.FixSuggestion);
            }

            var passCount = result.Checks.Count(c => c.Status == DoctorStatus.Pass);
            var failCount = result.Checks.Count(c => c.Status == DoctorStatus.Fail);
            var warnCount = result.Checks.Count(c => c.Status == DoctorStatus.Warning);

            Log.Information("");
            Log.Information("Doctor summary: {Pass} passed, {Fail} failed, {Warn} warnings",
                passCount, failCount, warnCount);

            if (result.FixedCount > 0)
                Log.Information("Auto-fixed {Count} issue(s)", result.FixedCount);

            if (result.FixableCount > 0 && !Fix)
                Log.Information("Run with --fix to auto-fix {Count} issue(s)", result.FixableCount);

            if (failCount > 0)
            {
                throw new Exception(
                    $"Doctor found {failCount} failing check(s). Fix the issues above and try again.");
            }
        });

    public static int Main()
    {
        AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", true);

        // Find and set root directory before Nuke initializes
        var rootDirectory = FindBuildConfigDirectory();
        Environment.SetEnvironmentVariable("NUKE_ROOT_DIRECTORY", rootDirectory);

        return Execute<Build>();
    }

    /// <summary>
    /// Walk up from current directory looking for build.config.json or build/build.config.json.
    /// </summary>
    private static string FindBuildConfigDirectory()
    {
        var current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            // Check for build.config.json in current directory
            if (File.Exists(Path.Combine(current, "build.config.json")))
            {
                return current;
            }

            // Check for build/build.config.json pattern
            var buildSubdir = Path.Combine(current, "build");
            if (File.Exists(Path.Combine(buildSubdir, "build.config.json")))
            {
                return current;
            }

            var parent = Directory.GetParent(current);
            current = parent?.FullName;
        }

        throw new InvalidOperationException(
            "Could not find build.config.json in current directory or any parent. " +
            "Ensure you are running from within a repository with a build.config.json file.");
    }
}
