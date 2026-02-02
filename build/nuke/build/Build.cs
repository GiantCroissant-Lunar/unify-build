using Nuke.Common;
using Nuke.Common.Tools.GitVersion;
using UnifyBuild.Nuke;

class Build : NukeBuild, IUnify
{
    [GitVersion]
    readonly GitVersion GitVersion;

    // Nuke resolves RootDirectory from the .nuke directory at the repo root.
    BuildContext IUnifyBuildConfig.UnifyConfig =>
        BuildContextLoader.FromJson(RootDirectory, "build.config.json");

    public static int Main() => Execute<Build>(x => ((IUnifyPack)x).PackProjects);
}
