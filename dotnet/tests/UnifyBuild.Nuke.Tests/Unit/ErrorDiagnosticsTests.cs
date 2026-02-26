using System.Text.Json;
using FluentAssertions;
using UnifyBuild.Nuke.Diagnostics;
using Xunit;

namespace UnifyBuild.Nuke.Tests.Unit;

/// <summary>
/// Tests for ErrorDiagnostics factory methods, error code assignment, formatting, and docs link generation.
/// Validates: Requirements 6.6
/// </summary>
public class ErrorDiagnosticsTests
{
    private const string DocsBaseUrl = "https://github.com/nicepkg/UnifyBuild/blob/main/docs/troubleshooting.md";

    #region FromJsonException

    [Fact]
    public void FromJsonException_CreatesConfigParseErrorDiagnostic()
    {
        var (ex, _) = CaptureJsonException("{invalid");

        var result = ErrorDiagnostics.FromJsonException(ex, "build.config.json");

        result.Code.Should().Be(ErrorCode.ConfigParseError);
        result.Message.Should().Contain("Failed to parse config");
        result.FilePath.Should().Be("build.config.json");
    }

    [Fact]
    public void FromJsonException_ExtractsLineAndColumnFromException()
    {
        var (ex, _) = CaptureJsonException("{invalid");

        var result = ErrorDiagnostics.FromJsonException(ex, "config.json");

        // JsonException.LineNumber is 0-based, converted to 1-based (+1)
        result.Line.Should().Be((int)(ex.LineNumber! + 1));
        result.Column.Should().Be((int?)ex.BytePositionInLine);
    }

    [Fact]
    public void FromJsonException_IncludesSuggestionAndDocsLink()
    {
        var (ex, _) = CaptureJsonException("{invalid");

        var result = ErrorDiagnostics.FromJsonException(ex, "config.json");

        result.Suggestion.Should().NotBeNullOrEmpty();
        result.Suggestion.Should().Contain("JSON syntax");
        result.DocsLink.Should().StartWith(DocsBaseUrl);
        result.DocsLink.Should().Contain("#config-parse-errors");
    }

    #endregion

    #region FromFileNotFound

    [Fact]
    public void FromFileNotFound_CreatesConfigNotFoundDiagnostic()
    {
        var ex = new FileNotFoundException("File not found", "build.config.json");
        var searchedPaths = new[] { "./build.config.json", "../build.config.json" };

        var result = ErrorDiagnostics.FromFileNotFound(ex, searchedPaths);

        result.Code.Should().Be(ErrorCode.ConfigNotFound);
    }

    [Fact]
    public void FromFileNotFound_IncludesSearchedPathsInMessage()
    {
        var ex = new FileNotFoundException("File not found", "build.config.json");
        var searchedPaths = new[] { "/root/build.config.json", "/home/build.config.json" };

        var result = ErrorDiagnostics.FromFileNotFound(ex, searchedPaths);

        result.Message.Should().Contain("/root/build.config.json");
        result.Message.Should().Contain("/home/build.config.json");
    }

    [Fact]
    public void FromFileNotFound_IncludesSuggestionToRunInit()
    {
        var ex = new FileNotFoundException("File not found", "build.config.json");

        var result = ErrorDiagnostics.FromFileNotFound(ex, new[] { "." });

        result.Suggestion.Should().Contain("init");
        result.DocsLink.Should().StartWith(DocsBaseUrl);
        result.DocsLink.Should().Contain("#missing-config");
    }

    #endregion

    #region FromBuildTargetFailure

    [Fact]
    public void FromBuildTargetFailure_CreatesBuildTargetFailedDiagnostic()
    {
        var ex = new InvalidOperationException("Compilation error");

        var result = ErrorDiagnostics.FromBuildTargetFailure("Compile", ex);

        result.Code.Should().Be(ErrorCode.BuildTargetFailed);
    }

    [Fact]
    public void FromBuildTargetFailure_IncludesTargetNameAndExceptionMessage()
    {
        var ex = new InvalidOperationException("Something went wrong");

        var result = ErrorDiagnostics.FromBuildTargetFailure("PackProjects", ex);

        result.Message.Should().Contain("PackProjects");
        result.Message.Should().Contain("Something went wrong");
    }

    [Fact]
    public void FromBuildTargetFailure_IncludesDocsLink()
    {
        var ex = new Exception("fail");

        var result = ErrorDiagnostics.FromBuildTargetFailure("Test", ex);

        result.DocsLink.Should().StartWith(DocsBaseUrl);
        result.DocsLink.Should().Contain("#build-target-failures");
    }

    #endregion

    #region FromNativeBuildFailure

    [Fact]
    public void FromNativeBuildFailure_CreatesNativeBuildFailedDiagnostic()
    {
        var result = ErrorDiagnostics.FromNativeBuildFailure("CMake Error at CMakeLists.txt:5");

        result.Code.Should().Be(ErrorCode.NativeBuildFailed);
    }

    [Fact]
    public void FromNativeBuildFailure_ExtractsFirstErrorLineFromCMakeOutput()
    {
        var cmakeOutput = """
            -- Configuring project
            -- Found package X
            CMake Error at CMakeLists.txt:5 (find_package): Could not find Boost
            -- Build failed
            """;

        var result = ErrorDiagnostics.FromNativeBuildFailure(cmakeOutput);

        result.Message.Should().Contain("CMake Error");
        result.Message.Should().Contain("Could not find Boost");
    }

    [Fact]
    public void FromNativeBuildFailure_ExtractsErrorColonPrefix()
    {
        var cmakeOutput = "info: building\nerror: linker failed\ndone";

        var result = ErrorDiagnostics.FromNativeBuildFailure(cmakeOutput);

        result.Message.Should().Contain("error: linker failed");
    }

    [Fact]
    public void FromNativeBuildFailure_HandlesEmptyOutput()
    {
        var result = ErrorDiagnostics.FromNativeBuildFailure("");

        result.Code.Should().Be(ErrorCode.NativeBuildFailed);
        result.Message.Should().Contain("Native build failed");
    }

    [Fact]
    public void FromNativeBuildFailure_HandlesOutputWithNoErrorLines()
    {
        var cmakeOutput = "-- Configuring done\n-- Build complete";

        var result = ErrorDiagnostics.FromNativeBuildFailure(cmakeOutput);

        result.Message.Should().Contain("Native build failed");
        result.DocsLink.Should().StartWith(DocsBaseUrl);
    }

    #endregion

    #region FormatDiagnostic

    [Fact]
    public void FormatDiagnostic_IncludesUBCodePrefix()
    {
        var msg = new DiagnosticMessage(
            ErrorCode.ConfigParseError, "parse error", "file.json", 1, 5, "fix it", $"{DocsBaseUrl}#x");

        var formatted = ErrorDiagnostics.FormatDiagnostic(msg);

        formatted.Should().StartWith("[UB101]");
    }

    [Fact]
    public void FormatDiagnostic_IncludesLocationWithLineAndColumn()
    {
        var msg = new DiagnosticMessage(
            ErrorCode.ConfigParseError, "bad json", "config.json", 10, 3, null, null);

        var formatted = ErrorDiagnostics.FormatDiagnostic(msg);

        formatted.Should().Contain("config.json(10,3)");
    }

    [Fact]
    public void FormatDiagnostic_IncludesLocationWithLineOnly()
    {
        var msg = new DiagnosticMessage(
            ErrorCode.ConfigNotFound, "not found", "config.json", 5, null, null, null);

        var formatted = ErrorDiagnostics.FormatDiagnostic(msg);

        formatted.Should().Contain("config.json(5)");
    }

    [Fact]
    public void FormatDiagnostic_IncludesFilePathOnlyWhenNoLineInfo()
    {
        var msg = new DiagnosticMessage(
            ErrorCode.ConfigNotFound, "not found", "config.json", null, null, null, null);

        var formatted = ErrorDiagnostics.FormatDiagnostic(msg);

        formatted.Should().Contain("config.json: not found");
    }

    [Fact]
    public void FormatDiagnostic_OmitsLocationWhenNoFilePath()
    {
        var msg = new DiagnosticMessage(
            ErrorCode.BuildTargetFailed, "target failed", null, null, null, null, null);

        var formatted = ErrorDiagnostics.FormatDiagnostic(msg);

        formatted.Should().Be("[UB200] target failed");
    }

    [Fact]
    public void FormatDiagnostic_IncludesSuggestionAndDocsLink()
    {
        var msg = new DiagnosticMessage(
            ErrorCode.ConfigParseError, "error", "f.json", 1, 1, "Try this", $"{DocsBaseUrl}#help");

        var formatted = ErrorDiagnostics.FormatDiagnostic(msg);

        formatted.Should().Contain("Suggestion: Try this");
        formatted.Should().Contain($"Docs: {DocsBaseUrl}#help");
    }

    #endregion

    #region Error Code Assignment

    [Fact]
    public void ErrorCode_ConfigParseError_Is101()
    {
        ((int)ErrorCode.ConfigParseError).Should().Be(101);
    }

    [Fact]
    public void ErrorCode_ConfigNotFound_Is100()
    {
        ((int)ErrorCode.ConfigNotFound).Should().Be(100);
    }

    [Fact]
    public void ErrorCode_BuildTargetFailed_Is200()
    {
        ((int)ErrorCode.BuildTargetFailed).Should().Be(200);
    }

    [Fact]
    public void ErrorCode_NativeBuildFailed_Is202()
    {
        ((int)ErrorCode.NativeBuildFailed).Should().Be(202);
    }

    #endregion

    #region Docs Link Generation

    [Fact]
    public void AllFactoryMethods_ProduceNonEmptyDocsLinks()
    {
        var jsonEx = CaptureJsonException("{bad").ex;
        var fileEx = new FileNotFoundException("nope", "x.json");

        var diagnostics = new[]
        {
            ErrorDiagnostics.FromJsonException(jsonEx, "f.json"),
            ErrorDiagnostics.FromFileNotFound(fileEx, new[] { "." }),
            ErrorDiagnostics.FromBuildTargetFailure("T", new Exception("e")),
            ErrorDiagnostics.FromNativeBuildFailure("CMake Error: fail"),
        };

        foreach (var diag in diagnostics)
        {
            diag.DocsLink.Should().NotBeNullOrEmpty();
            diag.DocsLink.Should().StartWith(DocsBaseUrl);
        }
    }

    [Fact]
    public void AllFactoryMethods_ProduceDocsLinksWithAnchors()
    {
        var jsonEx = CaptureJsonException("{bad").ex;
        var fileEx = new FileNotFoundException("nope", "x.json");

        var diagnostics = new[]
        {
            ErrorDiagnostics.FromJsonException(jsonEx, "f.json"),
            ErrorDiagnostics.FromFileNotFound(fileEx, new[] { "." }),
            ErrorDiagnostics.FromBuildTargetFailure("T", new Exception("e")),
            ErrorDiagnostics.FromNativeBuildFailure("output"),
        };

        foreach (var diag in diagnostics)
        {
            diag.DocsLink.Should().Contain("#", because: "docs links should include anchors");
        }
    }

    #endregion

    #region Helpers

    private static (JsonException ex, string input) CaptureJsonException(string invalidJson)
    {
        try
        {
            JsonSerializer.Deserialize<object>(invalidJson);
            throw new InvalidOperationException("Expected JsonException was not thrown");
        }
        catch (JsonException ex)
        {
            return (ex, invalidJson);
        }
    }

    #endregion
}
