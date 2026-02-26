using System;
using System.IO;
using Nuke.Common.Tooling;

namespace UnifyBuild.Nuke.PackageManagement;

/// <summary>
/// Generates Software Bill of Materials (SBOM) for packed packages.
/// Supports SPDX (via Microsoft sbom-tool) and CycloneDX formats.
/// </summary>
public sealed class SbomGenerator
{
    /// <summary>
    /// Result of an SBOM generation operation.
    /// </summary>
    public sealed record SbomResult(string OutputPath, string Format, bool Success, string? Error = null);

    /// <summary>
    /// Generates an SBOM for the given build output directory.
    /// </summary>
    public SbomResult Generate(SbomConfig config, string buildOutputDir, string packageName, string packageVersion)
    {
        var outputDir = config.OutputDir ?? Path.Combine(buildOutputDir, "_sbom");
        Directory.CreateDirectory(outputDir);

        var format = (config.Format ?? "spdx").ToLowerInvariant();

        return format switch
        {
            "spdx" => GenerateSpdx(buildOutputDir, outputDir, packageName, packageVersion),
            "cyclonedx" => GenerateCycloneDx(buildOutputDir, outputDir),
            _ => new SbomResult(outputDir, format, false, $"Unsupported SBOM format: {format}")
        };
    }

    private SbomResult GenerateSpdx(string buildOutputDir, string outputDir, string packageName, string packageVersion)
    {
        try
        {
            Serilog.Log.Information("Generating SPDX SBOM for {Package}", packageName);

            var arguments =
                $"generate -b \"{buildOutputDir}\" -bc \"{buildOutputDir}\" " +
                $"-pn \"{packageName}\" -pv \"{packageVersion}\" " +
                $"-ps \"UnifyBuild\" -m \"{outputDir}\"";

            ProcessTasks.StartProcess("sbom-tool", arguments)
                .AssertZeroExitCode();

            Serilog.Log.Information("SPDX SBOM generated at {OutputDir}", outputDir);
            return new SbomResult(outputDir, "spdx", true);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error("Failed to generate SPDX SBOM: {Error}", ex.Message);
            return new SbomResult(outputDir, "spdx", false, ex.Message);
        }
    }

    private SbomResult GenerateCycloneDx(string buildOutputDir, string outputDir)
    {
        try
        {
            Serilog.Log.Information("Generating CycloneDX SBOM");

            var outputFile = Path.Combine(outputDir, "bom.json");
            var arguments = $"\"{buildOutputDir}\" --output \"{outputFile}\" --json";

            ProcessTasks.StartProcess("dotnet-CycloneDX", arguments)
                .AssertZeroExitCode();

            Serilog.Log.Information("CycloneDX SBOM generated at {OutputFile}", outputFile);
            return new SbomResult(outputFile, "cyclonedx", true);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error("Failed to generate CycloneDX SBOM: {Error}", ex.Message);
            return new SbomResult(outputDir, "cyclonedx", false, ex.Message);
        }
    }
}
