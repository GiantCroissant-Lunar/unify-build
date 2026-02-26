using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using UnifyBuild.Nuke;
using UnifyBuild.Nuke.PackageManagement;
using Xunit;

namespace UnifyBuild.Nuke.Tests.Unit;

public class RetentionPolicyTests : IDisposable
{
    private readonly string _tempDir;

    public RetentionPolicyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"retention-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Apply_NoLocalFeedPath_ReturnsZeroRemoved()
    {
        var policy = new RetentionPolicy();
        var config = new RetentionPolicyConfig { MaxVersions = 2 };

        var result = policy.Apply(config);

        result.PackagesRemoved.Should().Be(0);
    }

    [Fact]
    public void Apply_NonExistentPath_ReturnsZeroRemoved()
    {
        var policy = new RetentionPolicy();
        var config = new RetentionPolicyConfig
        {
            LocalFeedPath = Path.Combine(_tempDir, "nonexistent"),
            MaxVersions = 2
        };

        var result = policy.Apply(config);

        result.PackagesRemoved.Should().Be(0);
    }

    [Fact]
    public void Apply_EmptyFeed_ReturnsZeroRemoved()
    {
        var policy = new RetentionPolicy();
        var config = new RetentionPolicyConfig
        {
            LocalFeedPath = _tempDir,
            MaxVersions = 2
        };

        var result = policy.Apply(config);

        result.PackagesRemoved.Should().Be(0);
    }

    [Fact]
    public void Apply_MaxVersions_RemovesOldestVersions()
    {
        // Create 4 fake packages for the same package ID
        CreateFakePackage("MyPackage.1.0.0.nupkg", TimeSpan.FromHours(4));
        CreateFakePackage("MyPackage.1.1.0.nupkg", TimeSpan.FromHours(3));
        CreateFakePackage("MyPackage.1.2.0.nupkg", TimeSpan.FromHours(2));
        CreateFakePackage("MyPackage.1.3.0.nupkg", TimeSpan.FromHours(1));

        var policy = new RetentionPolicy();
        var config = new RetentionPolicyConfig
        {
            LocalFeedPath = _tempDir,
            MaxVersions = 2
        };

        var result = policy.Apply(config);

        result.PackagesRemoved.Should().Be(2);
        // The 2 newest should remain
        Directory.GetFiles(_tempDir, "*.nupkg").Length.Should().Be(2);
    }

    [Fact]
    public void Apply_MaxAgeDays_RemovesOldPackages()
    {
        // Create a recent package and an old one
        CreateFakePackage("Recent.1.0.0.nupkg", TimeSpan.Zero);
        var oldFile = CreateFakePackage("Old.1.0.0.nupkg", TimeSpan.Zero);
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-60));

        var policy = new RetentionPolicy();
        var config = new RetentionPolicyConfig
        {
            LocalFeedPath = _tempDir,
            MaxAgeDays = 30
        };

        var result = policy.Apply(config);

        result.PackagesRemoved.Should().Be(1);
        result.RemovedFiles.Should().ContainSingle(f => f.Contains("Old.1.0.0.nupkg"));
    }

    [Fact]
    public void Apply_DifferentPackageIds_RetainsCorrectly()
    {
        CreateFakePackage("PackageA.1.0.0.nupkg", TimeSpan.FromHours(3));
        CreateFakePackage("PackageA.2.0.0.nupkg", TimeSpan.FromHours(2));
        CreateFakePackage("PackageA.3.0.0.nupkg", TimeSpan.FromHours(1));
        CreateFakePackage("PackageB.1.0.0.nupkg", TimeSpan.FromHours(2));
        CreateFakePackage("PackageB.2.0.0.nupkg", TimeSpan.FromHours(1));

        var policy = new RetentionPolicy();
        var config = new RetentionPolicyConfig
        {
            LocalFeedPath = _tempDir,
            MaxVersions = 2
        };

        var result = policy.Apply(config);

        // PackageA should lose 1 (oldest), PackageB should keep both
        result.PackagesRemoved.Should().Be(1);
        Directory.GetFiles(_tempDir, "*.nupkg").Length.Should().Be(4);
    }

    private string CreateFakePackage(string fileName, TimeSpan ageOffset)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, "fake-nupkg-content");
        if (ageOffset > TimeSpan.Zero)
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.Subtract(ageOffset));
        return path;
    }
}

public class ExtractPackageIdTests
{
    [Theory]
    [InlineData("MyPackage.1.0.0", "MyPackage")]
    [InlineData("My.Complex.Package.2.1.0", "My.Complex.Package")]
    [InlineData("SimplePackage.0.1.0-beta", "SimplePackage")]
    [InlineData("NoVersion", "NoVersion")]
    public void ExtractPackageId_ReturnsCorrectId(string input, string expected)
    {
        RetentionPolicy.ExtractPackageId(input).Should().Be(expected);
    }
}

public class PackageManagementConfigTests
{
    [Fact]
    public void PackageManagementConfig_DefaultValues()
    {
        var config = new PackageManagementConfig();

        config.Registries.Should().BeNull();
        config.Signing.Should().BeNull();
        config.Sbom.Should().BeNull();
        config.Retention.Should().BeNull();
    }

    [Fact]
    public void SbomConfig_DefaultFormat_IsSpdx()
    {
        var config = new SbomConfig();
        config.Format.Should().Be("spdx");
    }

    [Fact]
    public void NuGetRegistryConfig_DefaultValues()
    {
        var config = new NuGetRegistryConfig();
        config.Name.Should().BeEmpty();
        config.Url.Should().BeEmpty();
        config.ApiKeyEnvVar.Should().BeNull();
    }

    [Fact]
    public void PackageSigningConfig_DefaultValues()
    {
        var config = new PackageSigningConfig();
        config.CertificatePath.Should().BeNull();
        config.CertificatePasswordEnvVar.Should().BeNull();
        config.TimestampUrl.Should().BeNull();
    }

    [Fact]
    public void RetentionPolicyConfig_DefaultValues()
    {
        var config = new RetentionPolicyConfig();
        config.MaxVersions.Should().BeNull();
        config.MaxAgeDays.Should().BeNull();
        config.LocalFeedPath.Should().BeNull();
    }
}

public class PackagePublisherTests
{
    [Fact]
    public void PushToRegistries_EmptyDirectory_ReturnsEmptyResults()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"pub-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var publisher = new PackagePublisher();
            var registries = new[]
            {
                new NuGetRegistryConfig { Name = "Test", Url = "https://test.example.com", ApiKeyEnvVar = "TEST_KEY" }
            };

            var results = publisher.PushToRegistries(tempDir, registries);

            results.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void PushToRegistries_MissingApiKey_SkipsRegistry()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"pub-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "Test.1.0.0.nupkg"), "fake");

        try
        {
            var publisher = new PackagePublisher();
            var registries = new[]
            {
                new NuGetRegistryConfig
                {
                    Name = "Test",
                    Url = "https://test.example.com",
                    ApiKeyEnvVar = "NONEXISTENT_KEY_FOR_TESTING_12345"
                }
            };

            var results = publisher.PushToRegistries(tempDir, registries);

            results.Should().HaveCount(1);
            results[0].Success.Should().BeFalse();
            results[0].Error.Should().Contain("API key not configured");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}

public class PackageSignerTests
{
    [Fact]
    public void SignPackages_NoCertificatePath_ReturnsEmpty()
    {
        var signer = new PackageSigner();
        var config = new PackageSigningConfig();

        var results = signer.SignPackages("/some/dir", config);

        results.Should().BeEmpty();
    }

    [Fact]
    public void SignPackages_CertificateNotFound_ReturnsEmpty()
    {
        var signer = new PackageSigner();
        var config = new PackageSigningConfig
        {
            CertificatePath = "/nonexistent/cert.pfx"
        };

        var results = signer.SignPackages("/some/dir", config);

        results.Should().BeEmpty();
    }
}

public class SbomGeneratorTests
{
    [Fact]
    public void Generate_UnsupportedFormat_ReturnsFailure()
    {
        var generator = new SbomGenerator();
        var config = new SbomConfig { Format = "unknown" };
        var tempDir = Path.Combine(Path.GetTempPath(), $"sbom-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = generator.Generate(config, tempDir, "TestPackage", "1.0.0");

            result.Success.Should().BeFalse();
            result.Error.Should().Contain("Unsupported SBOM format");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
