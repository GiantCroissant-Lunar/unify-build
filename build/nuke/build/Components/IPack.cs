using System;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

/// <summary>
/// Packs projects into NuGet packages and optionally syncs them to a local feed.
///
/// Projects to pack are taken from:
/// 1. config.packProjectPaths (if non-empty), otherwise
/// 2. config.publishProjectPaths.
///
/// Packages are written to: build/_artifacts/{ArtifactsVersion}/nuget
/// </summary>
interface IPack : ICompile, IBuildConfig, IVersioning
{
    /// <summary>
    /// Directory where generated .nupkg files are written.
    /// </summary>
    AbsolutePath PackagesDirectory => PublishDirectory / "nuget";

    Target Pack => _ => _
        .DependsOn<IRestore>()
        .AssuredAfterFailure()
        .Executes(() =>
        {
            var cfg = Config;
            if (cfg == null)
            {
                Console.WriteLine("No build.config.json found or failed to parse; skipping pack.");
                return;
            }

            var packProjects =
                (cfg.PackProjectPaths != null && cfg.PackProjectPaths.Count > 0)
                    ? cfg.PackProjectPaths
                    : cfg.PublishProjectPaths;

            if (packProjects == null || packProjects.Count == 0)
            {
                Console.WriteLine("No PackProjectPaths or PublishProjectPaths configured; skipping pack.");
                return;
            }

            Directory.CreateDirectory(PackagesDirectory);

            foreach (var relativePath in packProjects)
            {
                var projectPath = RootDirectory / relativePath;
                var projectName = Path.GetFileNameWithoutExtension(projectPath);

                Console.WriteLine($"Packing {projectName} to {PackagesDirectory}...");

                DotNetPack(s => s
                    .SetProject(projectPath)
                    .SetConfiguration(Configuration)
                    .SetOutputDirectory(PackagesDirectory)
                    .EnableNoBuild());
            }

            // Optional local NuGet feed sync
            if (cfg.SyncLocalNugetFeed && !string.IsNullOrWhiteSpace(cfg.LocalNugetFeedRoot))
            {
                SyncLocalNugetFeed(cfg, PackagesDirectory);
            }
        });

    private static void SyncLocalNugetFeed(BuildConfig cfg, AbsolutePath packagesDirectory)
    {
        var root = cfg.LocalNugetFeedRoot;
        if (string.IsNullOrWhiteSpace(root))
            return;

        var flatDir = (AbsolutePath)Path.Combine(root, cfg.LocalNugetFeedFlatSubdir ?? "flat");
        var hierDir = (AbsolutePath)Path.Combine(root, cfg.LocalNugetFeedHierarchicalSubdir ?? "hierarchical");

        Directory.CreateDirectory(flatDir);
        Directory.CreateDirectory(hierDir);

        var nupkgFiles = Directory.Exists(packagesDirectory)
            ? Directory.GetFiles(packagesDirectory, "*.nupkg", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();

        foreach (var pkg in nupkgFiles)
        {
            var fileName = Path.GetFileName(pkg);

            // Flat layout: just copy all packages into a single directory
            File.Copy(pkg, flatDir / fileName, overwrite: true);

            // Simple hierarchical layout: group by package file name (without extension)
            var nameWithoutExt = Path.GetFileNameWithoutExtension(pkg);
            var destDir = hierDir / nameWithoutExt;
            Directory.CreateDirectory(destDir);
            File.Copy(pkg, destDir / fileName, overwrite: true);
        }

        Console.WriteLine($"Synced {nupkgFiles.Length} package(s) to local NuGet feed under {root}.");
    }
}
