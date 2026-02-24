namespace UnifyBuild.Nuke;

/// <summary>
/// Convenience interface that composes all UnifyBuild components.
/// Equivalent to the old UnifyBuildBase but as an interface.
/// </summary>
public interface IUnify : IUnifyPublish, IUnifyPack
{
}
