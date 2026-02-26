using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nuke.Common.Tooling;

namespace UnifyBuild.Nuke.PackageManagement;

/// <summary>
/// Signs NuGet packages using dotnet nuget sign.
/// </summary>
public sealed class PackageSigner
{
    /// <summary>
    /// Result of a signing operation.
    /// </summary>
    public sealed record SignResult(string PackagePath, bool Success, string? Error = null);

    /// <summary>
    /// Signs all .nupkg files in the given directory using the provided signing config.
    /// </summary>
    public IReadOnlyList<SignResult> SignPackages(string packageDirectory, PackageSigningConfig config)
    {
        var results = new List<SignResult>();

        if (string.IsNullOrEmpty(config.CertificatePath))
        {
            Serilog.Log.Warning("No certificate path configured, skipping package signing");
            return results;
        }

        if (!File.Exists(config.CertificatePath))
        {
            Serilog.Log.Error("Signing certificate not found at {Path}", config.CertificatePath);
            return results;
        }

        var password = !string.IsNullOrEmpty(config.CertificatePasswordEnvVar)
            ? Environment.GetEnvironmentVariable(config.CertificatePasswordEnvVar)
            : null;

        var packages = Directory.GetFiles(packageDirectory, "*.nupkg")
            .Where(p => !p.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (packages.Length == 0)
        {
            Serilog.Log.Warning("No .nupkg files found in {Directory}", packageDirectory);
            return results;
        }

        foreach (var package in packages)
        {
            var result = SignPackage(package, config, password);
            results.Add(result);
        }

        return results;
    }

    private SignResult SignPackage(string packagePath, PackageSigningConfig config, string? password)
    {
        try
        {
            Serilog.Log.Information("Signing {Package}", Path.GetFileName(packagePath));

            var arguments = $"nuget sign \"{packagePath}\" --certificate-path \"{config.CertificatePath}\" --overwrite";

            if (!string.IsNullOrEmpty(password))
                arguments += $" --certificate-password \"{password}\"";

            if (!string.IsNullOrEmpty(config.TimestampUrl))
                arguments += $" --timestamper \"{config.TimestampUrl}\"";

            ProcessTasks.StartProcess("dotnet", arguments)
                .AssertZeroExitCode();

            Serilog.Log.Information("Successfully signed {Package}", Path.GetFileName(packagePath));
            return new SignResult(packagePath, true);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error("Failed to sign {Package}: {Error}", Path.GetFileName(packagePath), ex.Message);
            return new SignResult(packagePath, false, ex.Message);
        }
    }
}
