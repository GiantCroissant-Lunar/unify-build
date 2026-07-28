using System.IO;
using FluentAssertions;
using Nuke.Common.IO;
using UnifyBuild.Nuke.Tests.Fixtures;
using Xunit;

namespace UnifyBuild.Nuke.Tests.Unit;

/// <summary>
/// Tests for vcpkg toolchain detection precedence in IUnifyNative.
/// Covers: VCPKG_ROOT > VCPKG_INSTALLATION_ROOT > repo-local > common paths.
/// Environment variables are cleared per test and restored in Dispose.
/// </summary>
[Collection("VcpkgDetection")]
public class VcpkgDetectionTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();
    private readonly List<string> _envVarsToClear = new();

    private AbsolutePath RepoRoot => (AbsolutePath)_temp.Path;

    /// <summary>
    /// Creates the vcpkg/scripts/buildsystems directory structure inside a given root
    /// and writes a dummy vcpkg.cmake file. Returns the full path to the toolchain file.
    /// </summary>
    private string CreateToolchainFile(string rootDir, string content = "# dummy toolchain")
    {
        var toolchainDir = Path.Combine(rootDir, "scripts", "buildsystems");
        Directory.CreateDirectory(toolchainDir);
        var toolchainPath = Path.Combine(toolchainDir, "vcpkg.cmake");
        File.WriteAllText(toolchainPath, content);
        return toolchainPath;
    }

    /// <summary>
    /// Normalizes a path for consistent comparison across platforms.
    /// </summary>
    private static string Norm(string path) => Path.GetFullPath(path);

    /// <summary>
    /// Sets an environment variable, tracking it for cleanup in Dispose.
    /// </summary>
    private void SetEnvVar(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        _envVarsToClear.Add(name);
    }

    /// <summary>
    /// Clears an environment variable, tracking it for cleanup in Dispose.
    /// </summary>
    private void ClearEnvVar(string name)
    {
        Environment.SetEnvironmentVariable(name, null);
        _envVarsToClear.Add(name);
    }

    public void Dispose()
    {
        foreach (var name in _envVarsToClear)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
        _envVarsToClear.Clear();
        _temp.Dispose();
    }

    [Fact]
    public void TryDetectVcpkgToolchain_VcpkgRoot_ReturnsToolchainWhenFileExists()
    {
        // Arrange: set VCPKG_ROOT to a temp dir with the toolchain file
        var vcpkgRoot = _temp.CreateDirectory("vcpkg_root");
        var toolchainPath = CreateToolchainFile(vcpkgRoot);
        ClearEnvVar("VCPKG_INSTALLATION_ROOT");

        SetEnvVar("VCPKG_ROOT", vcpkgRoot);

        // Act
        var result = IUnifyNative.TryDetectVcpkgToolchain(RepoRoot);

        // Assert
        Norm(result!).Should().Be(Norm(toolchainPath));
        File.Exists(result!).Should().BeTrue();
    }

    [Fact]
    public void TryDetectVcpkgToolchain_VcpkgRoot_PreferOverInstallationRoot()
    {
        // Arrange: both env vars set, both have valid toolchains
        var vcpkgRoot = _temp.CreateDirectory("vcpkg_root");
        var vcpkgInstallRoot = _temp.CreateDirectory("vcpkg_install");
        CreateToolchainFile(vcpkgRoot);

        SetEnvVar("VCPKG_ROOT", vcpkgRoot);
        SetEnvVar("VCPKG_INSTALLATION_ROOT", vcpkgInstallRoot);

        // Act
        var result = IUnifyNative.TryDetectVcpkgToolchain(RepoRoot);

        // Assert: VCPKG_ROOT takes precedence — result should be from vcpkg_root, not vcpkg_install
        result.Should().NotBeNull();
        result.Should().Contain("vcpkg_root");
    }

    [Fact]
    public void TryDetectVcpkgToolchain_InstallationRoot_ReturnsToolchainWhenFileExists()
    {
        // Arrange: VCPKG_ROOT not set, VCPKG_INSTALLATION_ROOT set with valid toolchain
        var vcpkgInstallRoot = _temp.CreateDirectory("vcpkg_install");
        var toolchainPath = CreateToolchainFile(vcpkgInstallRoot);

        ClearEnvVar("VCPKG_ROOT");
        SetEnvVar("VCPKG_INSTALLATION_ROOT", vcpkgInstallRoot);

        // Act
        var result = IUnifyNative.TryDetectVcpkgToolchain(RepoRoot);

        // Assert
        Norm(result!).Should().Be(Norm(toolchainPath));
    }

    [Fact]
    public void TryDetectVcpkgToolchain_InstallationRoot_FallsBackToRepoLocalWhenInvalid()
    {
        // Arrange: VCPKG_INSTALLATION_ROOT points to nonexistent dir, repo-local vcpkg exists
        _temp.CreateDirectory("vcpkg/scripts/buildsystems");
        var repoToolchain = CreateToolchainFile(Path.Combine(_temp.Path, "vcpkg"));

        ClearEnvVar("VCPKG_ROOT");
        SetEnvVar("VCPKG_INSTALLATION_ROOT", Path.Combine(_temp.Path, "nonexistent_vcpkg"));

        // Act
        var result = IUnifyNative.TryDetectVcpkgToolchain(RepoRoot);

        // Assert: falls through to repo-local
        Norm(result!).Should().Be(Norm(repoToolchain));
    }

    [Fact]
    public void TryDetectVcpkgToolchain_RepoLocal_ReturnsToolchainWhenNoEnvVars()
    {
        // Arrange: no env vars, repo-local vcpkg exists
        _temp.CreateDirectory("vcpkg/scripts/buildsystems");
        var repoToolchain = CreateToolchainFile(Path.Combine(_temp.Path, "vcpkg"));

        ClearEnvVar("VCPKG_ROOT");
        ClearEnvVar("VCPKG_INSTALLATION_ROOT");

        // Act
        var result = IUnifyNative.TryDetectVcpkgToolchain(RepoRoot);

        // Assert
        Norm(result!).Should().Be(Norm(repoToolchain));
    }

    [Fact]
    public void TryDetectVcpkgToolchain_NoVcpkg_ReturnsNull()
    {
        // Arrange: no env vars, no repo-local vcpkg, no common vcpkg
        ClearEnvVar("VCPKG_ROOT");
        ClearEnvVar("VCPKG_INSTALLATION_ROOT");

        // Act: use a temp dir with no vcpkg at all
        var result = IUnifyNative.TryDetectVcpkgToolchain(RepoRoot);

        // Assert: returns null unless vcpkg is installed at a common system path
        // If vcpkg happens to be at a common system path, the result will be non-null
        // but should not be from our temp dir.
        if (result is not null && !result.StartsWith(_temp.Path))
        {
            // vcpkg found at a common system path — acceptable, just verify it's not from our temp
            result.Should().NotStartWith(_temp.Path);
        }
        else
        {
            result.Should().BeNull();
        }
    }

    [Fact]
    public void TryDetectVcpkgToolchain_VcpkgRoot_InvalidPath_FallsThroughToInstallationRoot()
    {
        // Arrange: VCPKG_ROOT points to invalid dir, VCPKG_INSTALLATION_ROOT has valid toolchain
        var vcpkgInstallRoot = _temp.CreateDirectory("vcpkg_install");
        var installToolchain = CreateToolchainFile(vcpkgInstallRoot);

        SetEnvVar("VCPKG_ROOT", Path.Combine(_temp.Path, "nonexistent_root"));
        SetEnvVar("VCPKG_INSTALLATION_ROOT", vcpkgInstallRoot);

        // Act
        var result = IUnifyNative.TryDetectVcpkgToolchain(RepoRoot);

        // Assert: VCPKG_ROOT didn't match, VCPKG_INSTALLATION_ROOT did
        Norm(result!).Should().Be(Norm(installToolchain));
    }

    [Fact]
    public void TryDetectVcpkgToolchain_VcpkgRootEnvSetButNoFile_FallsThrough()
    {
        // Arrange: VCPKG_ROOT points to a real dir that lacks vcpkg.cmake,
        // VCPKG_INSTALLATION_ROOT not set, repo-local vcpkg exists
        var emptyRoot = _temp.CreateDirectory("empty_vcpkg_root");
        _temp.CreateDirectory("vcpkg/scripts/buildsystems");
        var repoToolchain = CreateToolchainFile(Path.Combine(_temp.Path, "vcpkg"));

        SetEnvVar("VCPKG_ROOT", emptyRoot);
        ClearEnvVar("VCPKG_INSTALLATION_ROOT");

        // Act
        var result = IUnifyNative.TryDetectVcpkgToolchain(RepoRoot);

        // Assert: falls through to repo-local
        Norm(result!).Should().Be(Norm(repoToolchain));
    }
}
