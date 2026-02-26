namespace UnifyBuild.Nuke.Diagnostics;

/// <summary>
/// Structured error record providing contextual information for build diagnostics.
/// </summary>
/// <param name="Code">Categorized error code (UB1xx–UB4xx).</param>
/// <param name="Message">Human-readable error description.</param>
/// <param name="FilePath">Optional path to the file where the error occurred.</param>
/// <param name="Line">Optional 1-based line number of the error location.</param>
/// <param name="Column">Optional 1-based column number of the error location.</param>
/// <param name="Suggestion">Optional actionable suggestion for resolving the error.</param>
/// <param name="DocsLink">Optional link to relevant documentation.</param>
public sealed record DiagnosticMessage(
    ErrorCode Code,
    string Message,
    string? FilePath,
    int? Line,
    int? Column,
    string? Suggestion,
    string? DocsLink
);
