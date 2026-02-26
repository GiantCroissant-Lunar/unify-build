namespace UnifyBuild.Nuke.Diagnostics;

/// <summary>
/// Categorized error codes for structured error handling across the UnifyBuild system.
/// UB1xx = Configuration, UB2xx = Build, UB3xx = Tooling, UB4xx = Schema.
/// </summary>
public enum ErrorCode
{
    // Config errors: UB1xx
    ConfigNotFound = 100,
    ConfigParseError = 101,
    ConfigSchemaViolation = 102,
    ConfigProjectNotFound = 103,
    ConfigDirNotFound = 104,
    ConfigDuplicateProject = 105,

    // Build errors: UB2xx
    BuildTargetFailed = 200,
    CompilationFailed = 201,
    NativeBuildFailed = 202,
    UnityBuildFailed = 203,

    // Tool errors: UB3xx
    ToolNotFound = 300,
    ToolVersionMismatch = 301,

    // Schema errors: UB4xx
    SchemaGenerationFailed = 400,
    SchemaValidationFailed = 401,
}
