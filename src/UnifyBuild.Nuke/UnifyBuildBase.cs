using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;

using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace UnifyBuild.Nuke;

[Obsolete("Use IUnify or individual component interfaces instead. This class will be removed in a future version.")]
public abstract class UnifyBuildBase : NukeBuild
{
    [Parameter("Configuration to build - Default is 'Release'")]
    public string Configuration { get; set; } = "Release";

    /// <summary>
    /// Concrete builds must provide their layout and (optionally) Version.
    /// </summary>
    protected abstract BuildContext Context { get; }

    /// <summary>
    /// Optionally build the solution if provided. This is a convenience wrapper.
    /// </summary>
    public Target Compile => _ => _
        .Executes(() =>
        {
            if (Context.Solution is null)
                return;

            DotNetBuild(s => s
                .SetProjectFile(Context.Solution)
                .SetConfiguration(Configuration));
        });

    public Target CompileProjects => _ => _
        .Executes(() =>
        {
            foreach (var project in Context.CompileProjects)
            {
                var projectPath = System.IO.Path.IsPathRooted(project)
                    ? project
                    : (Context.RepoRoot / project).ToString();

                DotNetBuild(s => s
                    .SetProjectFile(projectPath)
                    .SetConfiguration(Configuration));
            }
        });

    public Target PublishProjects => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            foreach (var project in Context.PublishProjects)
            {
                var projectPath = System.IO.Path.IsPathRooted(project)
                    ? project
                    : (Context.RepoRoot / project).ToString();

                DotNetPublish(s => ApplyCommonPublishSettings(s, projectPath));
            }
        });

    /// <summary>
    /// Publish all host projects under HostsDir. Relies on MSBuild targets
    /// (e.g. CopyHostArtifacts) to copy to build/_artifacts/{version}/hosts.
    /// </summary>
    public Target PublishHosts => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var projects = GetProjectsToPublish(Context.HostsDir, Context.IncludeHosts, Context.ExcludeHosts);
            foreach (var project in projects)
            {
                DotNetPublish(s => ApplyCommonPublishSettings(s, project));
            }
        });

    /// <summary>
    /// Publish all plugin projects under PluginsDir. Relies on MSBuild targets
    /// (e.g. CopyPluginArtifacts) to copy to build/_artifacts/{version}/plugins.
    /// </summary>
    public Target PublishPlugins => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var projects = GetProjectsToPublish(Context.PluginsDir, Context.IncludePlugins, Context.ExcludePlugins);
            foreach (var project in projects)
            {
                DotNetPublish(s => ApplyCommonPublishSettings(s, project));
            }
        });

    /// <summary>
    /// Synchronize the effective artifacts version folder to build/_artifacts/latest.
    /// This provides a stable path for tooling while keeping versioned artifacts immutable.
    /// </summary>
    public Target SyncLatestArtifacts => _ => _
        .DependsOn(PublishHosts, PublishPlugins)
        .Executes(() =>
        {
            var artifactsRoot = Context.RepoRoot / "build/_artifacts";
            var sourceVersion = Context.ArtifactsVersion ?? Context.Version ?? "local";
            var source = artifactsRoot / sourceVersion;
            var latest = artifactsRoot / "latest";

            if (!Directory.Exists(source))
                throw new InvalidOperationException($"Artifacts source '{source}' does not exist. Run publish targets first.");

            EnsureCleanDirectory(latest);
            CopyDirectoryRecursively(source, latest, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);
        });

    /// <summary>
    /// Pack all contract projects under ContractsDir into NuGet packages.
    /// Output goes to NuGetOutputDir (defaults to build/_artifacts/{version}/nuget).
    /// </summary>
    public Target PackContracts => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            if (Context.ContractsDir is null)
            {
                Serilog.Log.Warning("ContractsDir not configured - skipping PackContracts");
                return;
            }

            if (Context.NuGetOutputDir is null)
            {
                Serilog.Log.Warning("NuGetOutputDir not configured - skipping PackContracts");
                return;
            }

            EnsureExistingDirectory(Context.NuGetOutputDir);

            var projects = GetProjectsToPack(Context.ContractsDir, Context.IncludeContracts, Context.ExcludeContracts);
            foreach (var project in projects)
            {
                DotNetPack(s => ApplyCommonPackSettings(s, project, Context.NuGetOutputDir));
            }
        });

    /// <summary>
    /// Pack specific projects (from CompileProjects list) into NuGet packages.
    /// Useful when you want to pack a meta-package or specific projects.
    /// </summary>
    public Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            if (Context.NuGetOutputDir is null)
            {
                Serilog.Log.Warning("NuGetOutputDir not configured - skipping Pack");
                return;
            }

            EnsureExistingDirectory(Context.NuGetOutputDir);

            var projectsToPack = Context.PackProjects.Length > 0
                ? Context.PackProjects
                : Context.CompileProjects;

            // Pack projects
            foreach (var project in projectsToPack)
            {
                var projectPath = System.IO.Path.IsPathRooted(project)
                    ? project
                    : (Context.RepoRoot / project).ToString();

                if (File.Exists(projectPath))
                {
                    DotNetPack(s => ApplyCommonPackSettings(s, projectPath, Context.NuGetOutputDir));
                }
            }
        });

    public Target PackProjects => _ => _
        .DependsOn(Pack);

    /// <summary>
    /// Combined target: Pack contracts and then pack any additional projects.
    /// </summary>
    public Target PackAll => _ => _
        .DependsOn(PackContracts, Pack)
        .Executes(() =>
        {
            Serilog.Log.Information("All packages created in {OutputDir}", Context.NuGetOutputDir);
        });

    protected virtual DotNetPublishSettings ApplyCommonPublishSettings(DotNetPublishSettings settings, string project)
    {
        settings = settings
            .SetProject(project)
            .SetConfiguration(Configuration)
            ;

        if (!string.IsNullOrWhiteSpace(Context.Version))
        {
            settings = settings.SetProperty("Version", Context.Version);
        }

        if (!string.IsNullOrWhiteSpace(Context.ArtifactsVersion))
        {
            settings = settings.SetProperty("ArtifactsVersion", Context.ArtifactsVersion);
        }

        return settings;
    }

    protected virtual DotNetPackSettings ApplyCommonPackSettings(DotNetPackSettings settings, string project, AbsolutePath outputDir)
    {
        settings = settings
            .SetProject(project)
            .SetConfiguration(Configuration)
            .SetOutputDirectory(outputDir)
            .EnableNoRestore()
            .EnableIncludeSymbols()
            .SetSymbolPackageFormat(DotNetSymbolPackageFormat.snupkg);

        if (!string.IsNullOrWhiteSpace(Context.Version))
        {
            settings = settings.SetProperty("Version", Context.Version);
        }

        // Apply any custom pack properties from config
        foreach (var (key, value) in Context.PackProperties)
        {
            settings = settings.SetProperty(key, value);
        }

        return settings;
    }

    /// <summary>
    /// Get project paths to publish. If includes is specified, build paths directly from include list.
    /// Otherwise, search directory and apply excludes filter.
    /// </summary>
    private static IEnumerable<string> GetProjectsToPublish(
        string baseDir,
        string[] includes,
        string[] excludes)
    {
        if (includes.Length > 0)
        {
            // Build paths directly from include list - no directory search needed
            foreach (var name in includes)
            {
                var projectPath = Path.Combine(baseDir, name, $"{name}.csproj");
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
            // No includes specified - search all and apply excludes
            var allProjects = Directory.GetFiles(baseDir, "*.csproj", SearchOption.AllDirectories);
            foreach (var project in allProjects)
            {
                if (project.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    || project.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    || project.Contains($"{Path.AltDirectorySeparatorChar}obj{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    || project.Contains($"{Path.AltDirectorySeparatorChar}bin{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(project);
                if (excludes.Length == 0 || !excludes.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    yield return project;
                }
            }
        }
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
            // Build paths directly from include list
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
            // No includes specified - search all and apply excludes
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
