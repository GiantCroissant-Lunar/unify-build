using System.IO;
using Nuke.Common.IO;

namespace UnifyBuild.Nuke;

/// <summary>
/// Resolves the path to a unify-build configuration file using the same discovery rules
/// as normal build targets: prefer an explicit path, fall back to the repository root,
/// and finally check the build/ subdirectory.
/// </summary>
public static class BuildConfigResolver
{
    private const string DefaultConfigFileName = "build.config.json";

    /// <summary>
    /// Resolves the configuration file path for the given repository root.
    /// </summary>
    /// <param name="repoRoot">Repository root directory.</param>
    /// <param name="explicitConfigPath">
    /// Optional explicit config path, relative to the repository root or absolute.
    /// </param>
    /// <returns>
    /// The resolved configuration file path. If no configuration file is found,
    /// returns the root-level candidate path so callers can produce a consistent error.
    /// </returns>
    public static AbsolutePath ResolveConfigPath(AbsolutePath repoRoot, string? explicitConfigPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitConfigPath) && !IsDefaultConfigFileName(explicitConfigPath))
        {
            var path = ResolveExplicitPath(repoRoot, explicitConfigPath);
            if (File.Exists(path))
                return path;

            // For explicit paths, honor the candidate even when missing so the caller's
            // error message points at the path the user supplied.
            return path;
        }

        var rootPath = repoRoot / DefaultConfigFileName;
        if (File.Exists(rootPath))
            return rootPath;

        var buildDirPath = repoRoot / "build" / DefaultConfigFileName;
        if (File.Exists(buildDirPath))
            return buildDirPath;

        // Return the root candidate when nothing is found so error messages are consistent.
        return rootPath;
    }

    private static AbsolutePath ResolveExplicitPath(AbsolutePath repoRoot, string explicitConfigPath)
    {
        if (Path.IsPathRooted(explicitConfigPath))
            return (AbsolutePath)explicitConfigPath;

        return repoRoot / explicitConfigPath;
    }

    private static bool IsDefaultConfigFileName(string explicitConfigPath)
    {
        var fileName = Path.GetFileName(explicitConfigPath);
        if (!string.Equals(fileName, DefaultConfigFileName, StringComparison.OrdinalIgnoreCase))
            return false;

        // Only treat it as the default discovery name when no directory is specified.
        // Paths like "build/build.config.json" are explicit locations, not discovery.
        var directory = Path.GetDirectoryName(explicitConfigPath);
        return string.IsNullOrEmpty(directory);
    }
}
