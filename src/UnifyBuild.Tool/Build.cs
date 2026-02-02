using Nuke.Common;
using UnifyBuild.Nuke;

namespace UnifyBuild.Tool;

/// <summary>
/// Entry point for the unify-build CLI tool.
/// Composes all UnifyBuild components into a single NukeBuild class.
/// </summary>
class Build : NukeBuild, IUnify
{
    public static int Main() => Execute<Build>();
}
