using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nuke.Common.IO;

namespace UnifyBuild.Nuke.Performance;

/// <summary>
/// Detects source file changes for incremental builds by comparing
/// file timestamps against a build marker file.
/// </summary>
/// <remarks>
/// Usage with IUnifyCompile:
/// <code>
/// var detection = new ChangeDetection();
/// var markerDir = RootDirectory / "build" / "_markers";
/// var markerFile = markerDir / "MyProject.marker";
///
/// if (detection.HasChanges(projectDir, markerFile))
/// {
///     // Run compilation...
///     detection.UpdateMarker(markerFile);
/// }
/// else
/// {
///     Serilog.Log.Information("Skipping compilation — no changes detected");
/// }
/// </code>
/// </remarks>
public sealed class ChangeDetection
{
    /// <summary>
    /// Source file extensions to scan for changes.
    /// </summary>
    private static readonly string[] SourceExtensions = { ".cs", ".csproj", ".props", ".targets" };

    /// <summary>
    /// Directory names to exclude from scanning.
    /// </summary>
    private static readonly HashSet<string> ExcludedDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj"
    };

    /// <summary>
    /// Determines whether any source files in <paramref name="projectDir"/> have been
    /// modified since the <paramref name="markerFile"/> was last written.
    /// </summary>
    /// <param name="projectDir">The project directory to scan for source files.</param>
    /// <param name="markerFile">
    /// The build marker file whose last-write time represents the last successful build.
    /// If the marker file does not exist, this method returns <c>true</c> (first build).
    /// </param>
    /// <returns>
    /// <c>true</c> if the marker file does not exist or at least one source file
    /// has a last-write timestamp newer than the marker; <c>false</c> otherwise.
    /// </returns>
    public bool HasChanges(AbsolutePath projectDir, AbsolutePath markerFile)
    {
        var markerPath = (string)markerFile;

        if (!File.Exists(markerPath))
            return true;

        var markerTime = File.GetLastWriteTimeUtc(markerPath);

        return EnumerateSourceFiles(projectDir)
            .Any(file => File.GetLastWriteTimeUtc(file) > markerTime);
    }

    /// <summary>
    /// Creates or updates the build marker file with the current timestamp.
    /// Creates the parent directory if it does not exist.
    /// </summary>
    /// <param name="markerFile">The marker file path to create or touch.</param>
    public void UpdateMarker(AbsolutePath markerFile)
    {
        var markerPath = (string)markerFile;
        var directory = Path.GetDirectoryName(markerPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        // Touch the file: create it if missing, update timestamp if it exists
        File.WriteAllBytes(markerPath, Array.Empty<byte>());
        File.SetLastWriteTimeUtc(markerPath, DateTime.UtcNow);
    }

    /// <summary>
    /// Recursively enumerates source files in the given directory,
    /// excluding bin/ and obj/ subdirectories.
    /// </summary>
    internal static IEnumerable<string> EnumerateSourceFiles(AbsolutePath directory)
    {
        return EnumerateSourceFilesRecursive((string)directory);
    }

    private static IEnumerable<string> EnumerateSourceFilesRecursive(string directory)
    {
        if (!Directory.Exists(directory))
            yield break;

        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var ext = Path.GetExtension(file);
            if (SourceExtensions.Any(e => string.Equals(ext, e, StringComparison.OrdinalIgnoreCase)))
                yield return file;
        }

        foreach (var subDir in Directory.EnumerateDirectories(directory))
        {
            var dirName = Path.GetFileName(subDir);
            if (!ExcludedDirs.Contains(dirName))
            {
                foreach (var file in EnumerateSourceFilesRecursive(subDir))
                    yield return file;
            }
        }
    }
}
