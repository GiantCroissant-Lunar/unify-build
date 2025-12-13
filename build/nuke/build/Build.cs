using Nuke.Common;
using UnifyBuild.Nuke;

class Build : UnifyBuildBase
{
    protected override BuildContext Context =>
        BuildContextLoader.FromJson(RootDirectory / ".." / "..", "build.config.v2.json");

    public static int Main() => Execute<Build>(x => x.PackProjects);
}
