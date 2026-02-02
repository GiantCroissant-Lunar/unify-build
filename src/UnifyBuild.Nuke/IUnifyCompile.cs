using System.IO;
using Nuke.Common;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace UnifyBuild.Nuke;

/// <summary>
/// Compile targets: solution-level and explicit project compilation.
/// </summary>
public interface IUnifyCompile : IUnifyBuildConfig
{
    /// <summary>
    /// Compile the solution if configured.
    /// </summary>
    Target Compile => _ => _
        .Executes(() =>
        {
            if (UnifyConfig.Solution is null)
                return;

            DotNetBuild(s => s
                .SetProjectFile(UnifyConfig.Solution)
                .SetConfiguration(Configuration));
        });

    /// <summary>
    /// Compile specific projects from CompileProjects list.
    /// </summary>
    Target CompileProjects => _ => _
        .Executes(() =>
        {
            foreach (var project in UnifyConfig.CompileProjects)
            {
                var projectPath = Path.IsPathRooted(project)
                    ? project
                    : (UnifyConfig.RepoRoot / project).ToString();

                DotNetBuild(s => s
                    .SetProjectFile(projectPath)
                    .SetConfiguration(Configuration));
            }
        });
}
