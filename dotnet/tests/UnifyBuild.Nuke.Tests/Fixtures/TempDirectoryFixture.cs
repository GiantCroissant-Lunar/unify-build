using System;
using System.IO;

namespace UnifyBuild.Nuke.Tests.Fixtures;

/// <summary>
/// Creates a temporary directory on construction and deletes it on disposal.
/// Provides helper methods for creating files and subdirectories within the temp dir.
/// </summary>
public sealed class TempDirectoryFixture : IDisposable
{
    /// <summary>
    /// Full path to the temporary directory.
    /// </summary>
    public string Path { get; }

    public TempDirectoryFixture()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "UnifyBuildTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// Creates a subdirectory relative to the temp root and returns its full path.
    /// </summary>
    public string CreateDirectory(string relativePath)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    /// <summary>
    /// Creates a file with the given content relative to the temp root and returns its full path.
    /// Parent directories are created automatically.
    /// </summary>
    public string CreateFile(string relativePath, string content = "")
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        var dir = System.IO.Path.GetDirectoryName(fullPath);
        if (dir is not null)
            Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    /// <summary>
    /// Returns the full path for a relative path within the temp directory.
    /// Does not create the file or directory.
    /// </summary>
    public string GetPath(string relativePath)
        => System.IO.Path.Combine(Path, relativePath);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; ignore failures (e.g., locked files on Windows).
        }
    }
}
