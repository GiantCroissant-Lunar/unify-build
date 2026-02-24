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
    /// Alias target that depends on Pack.
    /// </summary>
    Target PackProjects => _ => _
        .DependsOn<IUnifyPack>(x => x.Pack);

    /// <summary>
    /// Combined target: Pack contracts and then pack any additional projects.
    /// </summary>
    Target PackAll => _ => _
        .DependsOn<IUnifyPack>(x => x.PackContracts)
        .DependsOn<IUnifyPack>(x => x.Pack)
        .Executes(() =>
        {
            Serilog.Log.Information("All packages created in {OutputDir}", UnifyConfig.NuGetOutputDir);
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
