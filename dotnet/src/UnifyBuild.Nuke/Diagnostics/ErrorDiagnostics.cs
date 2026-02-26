using System.Text.Json;

namespace UnifyBuild.Nuke.Diagnostics;

/// <summary>
/// Factory methods for creating structured <see cref="DiagnosticMessage"/> instances
/// from common exception types and build failures.
/// </summary>
public static class ErrorDiagnostics
{
    private const string DocsBaseUrl = "https://github.com/nicepkg/UnifyBuild/blob/main/docs/troubleshooting.md";

    /// <summary>
    /// Creates a diagnostic from a <see cref="JsonException"/> encountered while parsing a config file.
    /// Extracts line/column info and suggests checking JSON syntax.
    /// </summary>
    public static DiagnosticMessage FromJsonException(JsonException ex, string filePath)
    {
        return new DiagnosticMessage(
            Code: ErrorCode.ConfigParseError,
            Message: $"Failed to parse config: {ex.Message}",
            FilePath: filePath,
            Line: (int?)(ex.LineNumber + 1),  // JsonException.LineNumber is 0-based
            Column: (int?)ex.BytePositionInLine,
            Suggestion: "Check JSON syntax. Ensure all keys are quoted and trailing commas are removed.",
            DocsLink: $"{DocsBaseUrl}#config-parse-errors"
        );
    }

    /// <summary>
    /// Creates a diagnostic from a <see cref="FileNotFoundException"/> with the paths that were searched.
    /// </summary>
    public static DiagnosticMessage FromFileNotFound(FileNotFoundException ex, string[] searchedPaths)
    {
        var pathsList = string.Join(", ", searchedPaths);
        return new DiagnosticMessage(
            Code: ErrorCode.ConfigNotFound,
            Message: $"File not found: {ex.FileName ?? "unknown"}. Searched: {pathsList}",
            FilePath: ex.FileName,
            Line: null,
            Column: null,
            Suggestion: "Run 'dotnet unify-build init' to create a build config, or verify the file path.",
            DocsLink: $"{DocsBaseUrl}#missing-config"
        );
    }

    /// <summary>
    /// Creates a diagnostic for a build target failure.
    /// </summary>
    public static DiagnosticMessage FromBuildTargetFailure(string targetName, Exception ex)
    {
        return new DiagnosticMessage(
            Code: ErrorCode.BuildTargetFailed,
            Message: $"Target '{targetName}' failed: {ex.Message}",
            FilePath: null,
            Line: null,
            Column: null,
            Suggestion: $"Review the error above. Run with '--verbosity verbose' for detailed output.",
            DocsLink: $"{DocsBaseUrl}#build-target-failures"
        );
    }

    /// <summary>
    /// Creates a diagnostic for a native (CMake) build failure, extracting context from CMake output.
    /// </summary>
    public static DiagnosticMessage FromNativeBuildFailure(string cmakeOutput)
    {
        // Extract the most relevant error line from CMake output
        var errorLine = ExtractFirstErrorLine(cmakeOutput);
        var message = string.IsNullOrWhiteSpace(errorLine)
            ? "Native build failed. See CMake output above."
            : $"Native build failed: {errorLine}";

        return new DiagnosticMessage(
            Code: ErrorCode.NativeBuildFailed,
            Message: message,
            FilePath: null,
            Line: null,
            Column: null,
            Suggestion: "Check CMake configuration and ensure all dependencies are installed.",
            DocsLink: $"{DocsBaseUrl}#native-build-errors"
        );
    }

    /// <summary>
    /// Formats a <see cref="DiagnosticMessage"/> for console output with error code prefix.
    /// </summary>
    public static string FormatDiagnostic(DiagnosticMessage msg)
    {
        var code = $"UB{(int)msg.Code:D3}";
        var location = FormatLocation(msg.FilePath, msg.Line, msg.Column);
        var formatted = string.IsNullOrEmpty(location)
            ? $"[{code}] {msg.Message}"
            : $"[{code}] {location}: {msg.Message}";

        if (!string.IsNullOrEmpty(msg.Suggestion))
            formatted += $"\n  Suggestion: {msg.Suggestion}";

        if (!string.IsNullOrEmpty(msg.DocsLink))
            formatted += $"\n  Docs: {msg.DocsLink}";

        return formatted;
    }

    /// <summary>
    /// Logs an executed command at verbose level for diagnostics.
    /// </summary>
    public static void LogVerboseCommand(string tool, string arguments, string? workingDirectory = null)
    {
        var location = workingDirectory is not null ? $" in '{workingDirectory}'" : "";
        Serilog.Log.Verbose("Executing: {Tool} {Arguments}{Location}", tool, arguments, location);
    }

    /// <summary>
    /// Logs a <see cref="DiagnosticMessage"/> using structured Serilog logging with error code prefix.
    /// </summary>
    public static void LogDiagnostic(DiagnosticMessage msg)
    {
        var formatted = FormatDiagnostic(msg);
        var code = (int)msg.Code;

        if (code >= 200)
            Serilog.Log.Error(formatted);
        else
            Serilog.Log.Warning(formatted);
    }

    private static string FormatLocation(string? filePath, int? line, int? column)
    {
        if (string.IsNullOrEmpty(filePath))
            return string.Empty;

        if (line is not null && column is not null)
            return $"{filePath}({line},{column})";

        if (line is not null)
            return $"{filePath}({line})";

        return filePath;
    }

    private static string? ExtractFirstErrorLine(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("CMake Error", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }
        }

        return null;
    }
}
