using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Nuke.Common.IO;
using Serilog;
using UnifyBuild.Nuke.Validation;

namespace UnifyBuild.Nuke.Commands;

/// <summary>
/// Result of the validate command execution.
/// </summary>
public sealed record ValidateResult(
    bool IsValid,
    int ErrorCount,
    int WarningCount,
    IReadOnlyList<ValidationIssue> Issues
);

/// <summary>
/// Validates build.config.json against JSON Schema and performs semantic checks
/// such as verifying project references exist, source directories exist, and no duplicate projects.
/// Displays results with severity icons, line numbers, error codes, and suggestions.
/// </summary>
public sealed class ValidateCommand
{
    /// <summary>
    /// Executes validation: runs schema validation, then semantic validation if schema passes.
    /// Displays results with severity icons, line numbers, and suggestions.
    /// </summary>
    public ValidateResult Execute(AbsolutePath configPath, AbsolutePath repoRoot)
    {
        var path = (string)configPath;
        var validator = new ConfigValidator();
        var allIssues = new List<ValidationIssue>();

        // Step 1: Schema validation
        Log.Information("Validating schema for {ConfigPath}...", path);
        var schemaResult = validator.ValidateSchema(path);
        allIssues.AddRange(schemaResult.Issues);

        // Step 2: If schema is valid, also run semantic validation
        if (schemaResult.IsValid)
        {
            BuildJsonConfig? config = null;
            try
            {
                var json = File.ReadAllText(path);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                config = JsonSerializer.Deserialize<BuildJsonConfig>(json, options);
            }
            catch (Exception ex)
            {
                allIssues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "UB101",
                    $"Failed to parse configuration: {ex.Message}",
                    path
                ));
            }

            if (config is not null)
            {
                Log.Information("Running semantic validation...");
                var semanticResult = validator.ValidateSemantic(repoRoot, config);
                allIssues.AddRange(semanticResult.Issues);
            }
        }

        // Display results
        DisplayResults(allIssues);

        var errorCount = 0;
        var warningCount = 0;
        foreach (var issue in allIssues)
        {
            if (issue.Severity == ValidationSeverity.Error) errorCount++;
            else if (issue.Severity == ValidationSeverity.Warning) warningCount++;
        }

        var isValid = errorCount == 0;

        if (isValid)
            Log.Information("✓ Configuration is valid ({WarningCount} warning(s))", warningCount);
        else
            Log.Error("✗ Configuration has {ErrorCount} error(s) and {WarningCount} warning(s)", errorCount, warningCount);

        return new ValidateResult(isValid, errorCount, warningCount, allIssues);
    }

    private static void DisplayResults(List<ValidationIssue> issues)
    {
        foreach (var issue in issues)
        {
            var icon = issue.Severity switch
            {
                ValidationSeverity.Error => "✗",
                ValidationSeverity.Warning => "⚠",
                _ => "✓"
            };

            var location = issue.Line.HasValue
                ? $" (line {issue.Line})"
                : string.Empty;

            var message = $"{icon} [{issue.Code}]{location} {issue.Message}";

            switch (issue.Severity)
            {
                case ValidationSeverity.Error:
                    Log.Error(message);
                    break;
                case ValidationSeverity.Warning:
                    Log.Warning(message);
                    break;
                default:
                    Log.Information(message);
                    break;
            }

            if (!string.IsNullOrEmpty(issue.Suggestion))
                Log.Information("  → {Suggestion}", issue.Suggestion);
        }
    }
}
