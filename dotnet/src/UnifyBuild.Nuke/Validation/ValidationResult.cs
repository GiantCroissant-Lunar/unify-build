namespace UnifyBuild.Nuke.Validation;

/// <summary>
/// Severity level for configuration validation issues.
/// </summary>
public enum ValidationSeverity
{
    Error,
    Warning,
    Info
}

/// <summary>
/// A single validation issue found during schema or semantic validation.
/// </summary>
/// <param name="Severity">Issue severity level.</param>
/// <param name="Code">Error code (e.g., "UB102" for schema violations).</param>
/// <param name="Message">Human-readable description of the issue.</param>
/// <param name="FilePath">Optional path to the file where the issue was found.</param>
/// <param name="Line">Optional 1-based line number of the issue location.</param>
/// <param name="Suggestion">Optional actionable suggestion for resolving the issue.</param>
public sealed record ValidationIssue(
    ValidationSeverity Severity,
    string Code,
    string Message,
    string? FilePath = null,
    int? Line = null,
    string? Suggestion = null
);

/// <summary>
/// Result of a configuration validation operation, containing accumulated issues.
/// </summary>
/// <param name="IsValid">True if no errors were found (warnings and info are allowed).</param>
/// <param name="Issues">All validation issues found.</param>
public sealed record ValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationIssue> Issues
)
{
    /// <summary>
    /// Returns only error-level issues.
    /// </summary>
    public IEnumerable<ValidationIssue> Errors =>
        Issues.Where(i => i.Severity == ValidationSeverity.Error);

    /// <summary>
    /// Returns only warning-level issues.
    /// </summary>
    public IEnumerable<ValidationIssue> Warnings =>
        Issues.Where(i => i.Severity == ValidationSeverity.Warning);
}
