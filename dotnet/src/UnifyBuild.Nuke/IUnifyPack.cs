using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace UnifyBuild.Nuke;

/// <summary>
/// Pack targets: NuGet packaging for contracts, libraries, and explicit projects.
/// </summary>
public interface IUnifyPack : IUnifyCompile
{
    /// <summary>
    /// Pack all contract projects under ContractsDir into NuGet packages.
    /// </summary>
    Target PackContracts => _ => _
        .DependsOn<IUnifyCompile>(x => x.Compile)
        .Executes(() =>
        {
            if (UnifyConfig.ContractsDir is null)
            {
                Serilog.Log.Warning("ContractsDir not configured - skipping PackContracts");
                return;
            }

            if (UnifyConfig.NuGetOutputDir is null)
            {
                Serilog.Log.Warning("NuGetOutputDir not configured - skipping PackContracts");
                return;
            }

            UnifyConfig.NuGetOutputDir.CreateDirectory();

            var projects = GetProjectsToPack(UnifyConfig.ContractsDir, UnifyConfig.IncludeContracts, UnifyConfig.ExcludeContracts);
            foreach (var project in projects)
            {
                DotNetPack(s => ApplyCommonPackSettings(s, project, UnifyConfig.NuGetOutputDir));
            }
        });

    /// <summary>
    /// Pack specific projects into NuGet packages.
    /// </summary>
    Target Pack => _ => _
        .DependsOn<IUnifyCompile>(x => x.Compile)
        .TryDependsOn<IUnifySchemaGeneration>(x => x.GenerateSchema)
        .Executes(() =>
        {
            if (UnifyConfig.NuGetOutputDir is null)
            {
                Serilog.Log.Warning("NuGetOutputDir not configured - skipping Pack");
                return;
            }

            UnifyConfig.NuGetOutputDir.CreateDirectory();

            var projectsToPack = UnifyConfig.PackProjects.Length > 0
                ? UnifyConfig.PackProjects
                : UnifyConfig.CompileProjects;

            foreach (var project in projectsToPack)
            {
                var projectPath = Path.IsPathRooted(project)
                    ? project
                    : (UnifyConfig.RepoRoot / project).ToString();

                if (File.Exists(projectPath))
                {
                    DotNetPack(s => ApplyCommonPackSettings(s, projectPath, UnifyConfig.NuGetOutputDir));
                }
            }
        });

    /// <summary>
    /// Alias target that depends on Pack and triggers SyncLocalFeed when configured.
    /// </summary>
    Target PackProjects => _ => _
        .DependsOn<IUnifyPack>(x => x.Pack)
        .TryDependsOn<IUnifyPack>(x => x.SyncLocalFeed);

    /// <summary>
    /// Combined target: Pack contracts, pack any additional projects, sync to local feed.
    /// </summary>
    Target PackAll => _ => _
        .DependsOn<IUnifyPack>(x => x.PackContracts)
        .DependsOn<IUnifyPack>(x => x.Pack)
        .TryDependsOn<IUnifyPack>(x => x.SyncLocalFeed)
        .Executes(() =>
        {
            Serilog.Log.Information("All packages created in {OutputDir}", UnifyConfig.NuGetOutputDir);
        });

    /// <summary>
    /// Sync packed .nupkg files from NuGetOutputDir to UnifyConfig.LocalNugetFeedRoot
    /// (top-level flat layout). Runs only when build.config.json sets
    /// "syncLocalNugetFeed": true AND "localNugetFeedRoot": "...".
    ///
    /// Writes to the feed root directly — not to a "flat" or "hierarchical" subdir.
    /// This matches the post-2026-05-19 workspace convention where the feed root IS
    /// the flat-layout feed; legacy subdir conventions are retired.
    /// </summary>
    Target SyncLocalFeed => _ => _
        .DependsOn<IUnifyPack>(x => x.Pack)
        .OnlyWhenStatic(() => UnifyConfig.SyncLocalNugetFeed)
        .Executes(() =>
        {
            if (UnifyConfig.LocalNugetFeedRoot is null)
            {
                Serilog.Log.Warning(
                    "syncLocalNugetFeed=true but localNugetFeedRoot is null - skipping local feed sync");
                return;
            }

            if (UnifyConfig.NuGetOutputDir is null)
            {
                Serilog.Log.Warning("NuGetOutputDir is null - skipping local feed sync");
                return;
            }

            var sourceDir = UnifyConfig.NuGetOutputDir;
            if (!Directory.Exists(sourceDir))
            {
                Serilog.Log.Warning("Pack output directory {Source} does not exist - skipping local feed sync", sourceDir);
                return;
            }

            var feedRoot = UnifyConfig.LocalNugetFeedRoot;
            feedRoot.CreateDirectory();

            var nupkgs = Directory.GetFiles(sourceDir, "*.nupkg", SearchOption.TopDirectoryOnly);
            foreach (var pkg in nupkgs)
            {
                var dest = feedRoot / Path.GetFileName(pkg);
                File.Copy(pkg, dest, overwrite: true);
            }

            Serilog.Log.Information(
                "Synced {Count} package(s) from {Source} to local feed {Feed}",
                nupkgs.Length, sourceDir, feedRoot);
        });

    /// <summary>
    /// Apply common pack settings including version, output directory, and custom properties.
    /// </summary>
    private DotNetPackSettings ApplyCommonPackSettings(DotNetPackSettings settings, string project, AbsolutePath outputDir)
    {
        settings = settings
            .SetProject(project)
            .SetConfiguration(Configuration)
            .SetOutputDirectory(outputDir)
            .EnableNoRestore();

        if (UnifyConfig.PackIncludeSymbols)
        {
            settings = settings
                .EnableIncludeSymbols()
                .SetSymbolPackageFormat(DotNetSymbolPackageFormat.snupkg);
        }

        if (!string.IsNullOrWhiteSpace(UnifyConfig.Version))
        {
            settings = settings.SetProperty("Version", UnifyConfig.Version);
        }

        foreach (var (key, value) in UnifyConfig.PackProperties)
        {
            settings = settings.SetProperty(key, value);
        }

        return settings;
    }

    /// <summary>
    /// Get project paths to pack. Similar to GetProjectsToPublish but works with AbsolutePath.
    /// </summary>
    private static IEnumerable<string> GetProjectsToPack(
        AbsolutePath baseDir,
        string[] includes,
        string[] excludes)
    {
        if (!Directory.Exists(baseDir))
        {
            Serilog.Log.Warning("Contracts directory does not exist: {BaseDir}", baseDir);
            yield break;
        }

        if (includes.Length > 0)
        {
            foreach (var name in includes)
            {
                var projectPath = baseDir / name / $"{name}.csproj";
                if (File.Exists(projectPath))
                {
                    if (excludes.Length == 0 || !excludes.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        yield return projectPath;
                    }
                }
            }
        }
        else
        {
            var allProjects = Directory.GetFiles(baseDir, "*.csproj", SearchOption.AllDirectories);
            foreach (var project in allProjects)
            {
                var name = Path.GetFileNameWithoutExtension(project);
                if (excludes.Length == 0 || !excludes.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    yield return project;
                }
            }
        }
    }
}
