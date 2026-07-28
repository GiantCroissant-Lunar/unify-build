using System.IO;
using FluentAssertions;
using Nuke.Common.IO;
using UnifyBuild.Nuke.Tests.Fixtures;
using Xunit;

namespace UnifyBuild.Nuke.Tests.Unit;

/// <summary>
/// xUnit collection definition that disables parallelization for tests
/// that mutate process-wide environment variables.
/// </summary>
[CollectionDefinition("VcpkgDetection", DisableParallelization = true)]
public class VcpkgDetectionCollectionDefinition;

/// <summary>
/// Tests for vcpkg toolchain detection precedence in IUnifyNative.
/// Covers: VCPKG_ROOT > VCPKG_INSTALLATION_ROOT > repo-local > common paths.
/// Each test saves the original environment variables and restores them in Dispose
/// so that process-wide state is genuinely isolated.
/// </summary>
[Collection("VcpkgDetection")]
public class VcpkgDetectionTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();

    // Captured original environment variable values (may be null).
    private readonly string? _originalVcpkgRoot = Environment.GetEnvironmentVariable("VCPKG_ROOT");
    private readonly string? _originalVcpkgInstallationRoot = Environment.GetEnvironmentVariable("VCPKG_INSTALLATION_ROOT");

    private AbsolutePath RepoRoot => (AbsolutePath)_temp.Path;

    /// <summary>
    /// Creates the vcpkg/scripts/buildsystems directory structure inside a given root
    /// and writes a dummy vcpkg.cmake file. Returns the full path to the toolchain file.
    /// </summary>
    private static string CreateToolchainFile(string rootDir, string content = "# dummy toolchain")
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
    /// Sets an environment variable. Dispose will restore the original value.
    /// </summary>
    private static void SetEnvVar(string name, string value)
        => Environment.SetEnvironmentVariable(name, value);

    /// <summary>
    /// Clears an environment variable. Dispose will restore the original value.
    /// </summary>
    private static void ClearEnvVar(string name)
        => Environment.SetEnvironmentVariable(name, null);

    public void Dispose()
    {
        // Restore original environment variable values so other test classes
        // (and the rest of the process) see their pre-test state.
        Environment.SetEnvironmentVariable("VCPKG_ROOT", _originalVcpkgRoot);
        Environment.SetEnvironmentVariable("VCPKG_INSTALLATION_ROOT", _originalVcpkgInstallationRoot);
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
        // Arrange: both env vars set, both have valid toolchain files
        var vcpkgRoot = _temp.CreateDirectory("vcpkg_root");
        var vcpkgInstallRoot = _temp.CreateDirectory("vcpkg_install");
        var rootToolchain = CreateToolchainFile(vcpkgRoot);
        CreateToolchainFile(vcpkgInstallRoot);

        SetEnvVar("VCPKG_ROOT", vcpkgRoot);
        SetEnvVar("VCPKG_INSTALLATION_ROOT", vcpkgInstallRoot);

        // Act
        var result = IUnifyNative.TryDetectVcpkgToolchain(RepoRoot);

        // Assert: VCPKG_ROOT takes precedence — result must be the VCPKG_ROOT toolchain
        result.Should().NotBeNull();
        Norm(result!).Should().Be(Norm(rootToolchain));
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
        // Arrange: no env vars, no repo-local vcpkg
        ClearEnvVar("VCPKG_ROOT");
        ClearEnvVar("VCPKG_INSTALLATION_ROOT");

        // Act: use a temp dir with no vcpkg structure at all
        var result = IUnifyNative.TryDetectVcpkgToolchain(RepoRoot);

        // Assert: the result must either be null (no vcpkg anywhere) or
        // point to a common system path outside our temp directory.
        // We cannot assert null unconditionally because vcpkg might be
        // installed at a common system path on the test machine.
        if (result is not null)
        {
            result.Should().NotStartWith(_temp.Path, because: "our temp dir has no vcpkg structure");
        }
    }

    [Fact]
    public void TryDetectVcpkgToolchain_VcpkgRoot_InvalidPath_FallsThroughToInstallationRoot()
    {
        // Arrange: VCPKG_ROOT points to nonexistent dir, VCPKG_INSTALLATION_ROOT has valid toolchain
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

    [Fact]
    public void TryDetectVcpkgToolchain_InstallationRootEnvSetButNoFile_FallsThroughToRepoLocal()
    {
        // Arrange: VCPKG_ROOT not set, VCPKG_INSTALLATION_ROOT points to real dir
        // that lacks vcpkg.cmake, repo-local vcpkg exists
        _temp.CreateDirectory("vcpkg/scripts/buildsystems");
        var repoToolchain = CreateToolchainFile(Path.Combine(_temp.Path, "vcpkg"));
        var emptyInstallRoot = _temp.CreateDirectory("empty_install_root");

        ClearEnvVar("VCPKG_ROOT");
        SetEnvVar("VCPKG_INSTALLATION_ROOT", emptyInstallRoot);

        // Act
        var result = IUnifyNative.TryDetectVcpkgToolchain(RepoRoot);

        // Assert: falls through to repo-local
        Norm(result!).Should().Be(Norm(repoToolchain));
    }
}
