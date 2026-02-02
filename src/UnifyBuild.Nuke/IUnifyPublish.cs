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

/// <summary>
/// Publish targets: hosts, plugins, and explicit project publishing.
/// </summary>
public interface IUnifyPublish : IUnifyCompile
{
    /// <summary>
    /// Publish all host projects under HostsDir.
    /// </summary>
    Target PublishHosts => _ => _
        .DependsOn<IUnifyCompile>(x => x.Compile)
        .Executes(() =>
        {
            var projects = GetProjectsToPublish(UnifyConfig.HostsDir, UnifyConfig.IncludeHosts, UnifyConfig.ExcludeHosts);
            foreach (var project in projects)
            {
                DotNetPublish(s => ApplyCommonPublishSettings(s, project));
            }
        });

    /// <summary>
    /// Publish all plugin projects under PluginsDir.
    /// </summary>
    Target PublishPlugins => _ => _
        .DependsOn<IUnifyCompile>(x => x.Compile)
        .Executes(() =>
        {
            var projects = GetProjectsToPublish(UnifyConfig.PluginsDir, UnifyConfig.IncludePlugins, UnifyConfig.ExcludePlugins);
            foreach (var project in projects)
            {
                DotNetPublish(s => ApplyCommonPublishSettings(s, project));
            }
        });

    /// <summary>
    /// Publish specific projects from PublishProjects list.
    /// </summary>
    Target PublishProjects => _ => _
        .DependsOn<IUnifyCompile>(x => x.Compile)
        .Executes(() =>
        {
            foreach (var project in UnifyConfig.PublishProjects)
            {
                var projectPath = Path.IsPathRooted(project)
                    ? project
                    : (UnifyConfig.RepoRoot / project).ToString();

                DotNetPublish(s => ApplyCommonPublishSettings(s, projectPath));
            }
        });

    /// <summary>
    /// Synchronize the effective artifacts version folder to build/_artifacts/latest.
    /// </summary>
    Target SyncLatestArtifacts => _ => _
        .DependsOn<IUnifyPublish>(x => x.PublishHosts)
        .DependsOn<IUnifyPublish>(x => x.PublishPlugins)
        .Executes(() =>
        {
            var artifactsRoot = UnifyConfig.RepoRoot / "build" / "_artifacts";
            var sourceVersion = UnifyConfig.ArtifactsVersion ?? UnifyConfig.Version ?? "local";
            var source = artifactsRoot / sourceVersion;
            var latest = artifactsRoot / "latest";

            if (!Directory.Exists(source))
                throw new InvalidOperationException($"Artifacts source '{source}' does not exist. Run publish targets first.");

            latest.CreateOrCleanDirectory();
            CopyDirectoryRecursively(source, latest, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);
        });

    /// <summary>
    /// Apply common publish settings including version and artifacts version.
    /// </summary>
    private DotNetPublishSettings ApplyCommonPublishSettings(DotNetPublishSettings settings, string project)
    {
        settings = settings
            .SetProject(project)
            .SetConfiguration(Configuration);

        if (!string.IsNullOrWhiteSpace(UnifyConfig.Version))
        {
            settings = settings.SetProperty("Version", UnifyConfig.Version);
        }

        if (!string.IsNullOrWhiteSpace(UnifyConfig.ArtifactsVersion))
        {
            settings = settings.SetProperty("ArtifactsVersion", UnifyConfig.ArtifactsVersion);
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
}
