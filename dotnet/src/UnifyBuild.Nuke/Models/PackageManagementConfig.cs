namespace UnifyBuild.Nuke;

/// <summary>
/// Advanced package management configuration.
/// </summary>
public sealed class PackageManagementConfig
{
    /// <summary>
    /// NuGet registries to publish packages to.
    /// </summary>
    public NuGetRegistryConfig[]? Registries { get; set; }

    /// <summary>
    /// NuGet package signing configuration.
    /// </summary>
    public PackageSigningConfig? Signing { get; set; }

    /// <summary>
    /// SBOM generation configuration.
    /// </summary>
    public SbomConfig? Sbom { get; set; }

    /// <summary>
    /// Package retention policy for local feed cleanup.
    /// </summary>
    public RetentionPolicyConfig? Retention { get; set; }
}

/// <summary>
/// Configuration for a NuGet registry endpoint.
/// </summary>
public sealed class NuGetRegistryConfig
{
    /// <summary>
    /// Display name for the registry (e.g., "NuGet.org", "GitHub Packages").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Registry URL (e.g., "https://api.nuget.org/v3/index.json").
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Name of the environment variable containing the API key for this registry.
    /// </summary>
    public string? ApiKeyEnvVar { get; set; }
}

/// <summary>
/// Configuration for NuGet package signing.
/// </summary>
public sealed class PackageSigningConfig
{
    /// <summary>
    /// Path to the signing certificate (.pfx).
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Name of the environment variable containing the certificate password.
    /// </summary>
    public string? CertificatePasswordEnvVar { get; set; }

    /// <summary>
    /// Timestamp server URL for countersigning (e.g., "http://timestamp.digicert.com").
    /// </summary>
    public string? TimestampUrl { get; set; }
}

/// <summary>
/// Configuration for SBOM generation.
/// </summary>
public sealed class SbomConfig
{
    /// <summary>
    /// SBOM format: "spdx" or "cyclonedx".
    /// </summary>
    public string Format { get; set; } = "spdx";

    /// <summary>
    /// Output directory for generated SBOM files.
    /// </summary>
    public string? OutputDir { get; set; }
}

/// <summary>
/// Configuration for package retention policy on local feeds.
/// </summary>
public sealed class RetentionPolicyConfig
{
    /// <summary>
    /// Maximum number of versions to keep per package.
    /// </summary>
    public int? MaxVersions { get; set; }

    /// <summary>
    /// Maximum age in days for packages. Older packages are removed.
    /// </summary>
    public int? MaxAgeDays { get; set; }

    /// <summary>
    /// Path to the local NuGet feed directory to apply retention to.
    /// </summary>
    public string? LocalFeedPath { get; set; }
}
