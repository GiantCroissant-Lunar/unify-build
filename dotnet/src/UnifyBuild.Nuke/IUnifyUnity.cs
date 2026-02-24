using System;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace UnifyBuild.Nuke;

/// <summary>
/// Component for building netstandard2.1 DLLs and copying them into Unity package directories.
/// </summary>
public interface IUnifyUnity : IUnifyCompile
{
    /// <summary>
    /// Build netstandard2.1 projects and copy output DLLs to Unity package Runtime/ directories.
    /// </summary>
    Target BuildForUnity => _ => _
        .Description("Build netstandard2.1 DLLs and copy to Unity packages")
        .Executes(() =>
        {
            var unityConfig = UnifyConfig.UnityBuild;
            if (unityConfig is null)
            {
                Serilog.Log.Information("No Unity build configuration found. Skipping.");
                return;
            }

            Serilog.Log.Information("Building for Unity: {PackageCount} packages, framework={Framework}",
                unityConfig.Packages.Length, unityConfig.TargetFramework);

            // Phase 1: Build all source projects
            var allProjects = unityConfig.Packages
                .SelectMany(p => p.SourceProjects)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var project in allProjects)
            {
                var projectPath = Path.IsPathRooted(project)
                    ? project
                    : (UnifyConfig.RepoRoot / project).ToString();

                Serilog.Log.Information("Building {Project} for {Framework}", project, unityConfig.TargetFramework);

                DotNetBuild(s => s
                    .SetProjectFile(projectPath)
                    .SetConfiguration(Configuration)
                    .SetFramework(unityConfig.TargetFramework));
            }

            // Phase 2: Copy DLLs to Unity package directories
            foreach (var mapping in unityConfig.Packages)
            {
                var packageRuntimeDir = unityConfig.UnityProjectRoot
                    / "project" / "packages" / mapping.ScopedIndex / mapping.PackageName / "Runtime";

                EnsureExistingDirectory(packageRuntimeDir);

                // Copy build output DLLs from source projects
                foreach (var project in mapping.SourceProjects)
                {
                    var projectPath = Path.IsPathRooted(project)
                        ? project
                        : (UnifyConfig.RepoRoot / project).ToString();

                    var projectDir = Path.GetDirectoryName(projectPath)!;
                    var projectName = Path.GetFileNameWithoutExtension(projectPath);
                    var outputDir = Path.Combine(projectDir, "bin", Configuration, unityConfig.TargetFramework);

                    var dllPath = Path.Combine(outputDir, projectName + ".dll");
                    if (File.Exists(dllPath))
                    {
                        var destPath = packageRuntimeDir / (projectName + ".dll");
                        File.Copy(dllPath, destPath, overwrite: true);
                        Serilog.Log.Information("  Copied {Dll} → {Package}", projectName + ".dll", mapping.PackageName);
                    }
                    else
                    {
                        Serilog.Log.Warning("  DLL not found: {Path}", dllPath);
                    }
                }

                // Copy specified dependency DLLs
                if (mapping.DependencyDlls.Length > 0)
                {
                    CopyDependencyDlls(unityConfig, mapping, packageRuntimeDir);
                }
            }

            Serilog.Log.Information("Unity package DLL copy complete.");
        });

    private void CopyDependencyDlls(UnityBuildContext unityConfig, UnityPackageMapping mapping, AbsolutePath packageRuntimeDir)
    {
        // Collect all build output directories from source projects across all mappings
        // to search for transitive dependency DLLs
        var searchDirs = unityConfig.Packages
            .SelectMany(p => p.SourceProjects)
            .Select(project =>
            {
                var projectPath = Path.IsPathRooted(project)
                    ? project
                    : (UnifyConfig.RepoRoot / project).ToString();
                var projectDir = Path.GetDirectoryName(projectPath)!;
                return Path.Combine(projectDir, "bin", Configuration, unityConfig.TargetFramework);
            })
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var dllName in mapping.DependencyDlls)
        {
            var found = false;
            foreach (var searchDir in searchDirs)
            {
                var dllPath = Path.Combine(searchDir, dllName);
                if (File.Exists(dllPath))
                {
                    var destPath = packageRuntimeDir / dllName;
                    File.Copy(dllPath, destPath, overwrite: true);
                    Serilog.Log.Information("  Copied dep {Dll} → {Package}", dllName, mapping.PackageName);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Serilog.Log.Warning("  Dependency DLL not found in any build output: {Dll}", dllName);
            }
        }
    }
}
