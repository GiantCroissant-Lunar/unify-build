using Nuke.Common;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.Git;
using UnifyBuild.Nuke;
using System;
using System.Linq;

class Build : NukeBuild, IUnify
{
    // GitVersion is optional - falls back to computed version if not available
    [GitVersion(UpdateAssemblyInfo = false)]
    readonly GitVersion GitVersion;

    BuildContext IUnifyBuildConfig.UnifyConfig =>
        BuildContextLoader.FromJson(RootDirectory, "build.config.json", GetVersionOrDefault());

    private string GetVersionOrDefault()
    {
        // Use GitVersion if available
        if (GitVersion != null)
            return GitVersion.MajorMinorPatch;

        // Try git describe as fallback
        try
        {
            var result = GitTasks.Git("describe --tags --always 2>nul", logOutput: false);
            if (result.Count > 0)
            {
                var first = result.First();
                if (!string.IsNullOrWhiteSpace(first.Text))
                {
                    var tag = first.Text.Trim();
                    // Convert git describe output to version (e.g., "v1.2.3-5-gabc123" -> "1.2.3")
                    if (tag.StartsWith("v") || tag.StartsWith("V"))
                        tag = tag.Substring(1);
                    var dashIndex = tag.IndexOf('-');
                    if (dashIndex > 0)
                        tag = tag.Substring(0, dashIndex);
                    if (Version.TryParse(tag, out _))
                        return tag;
                }
            }
        }
        catch { /* ignore git errors */ }

        // Return null to let BuildConfigJson use its own defaults
        return null;
    }

    public static int Main() => Execute<Build>(x => ((IUnifyPack)x).PackProjects);
}
