using Nuke.Common;
using Nuke.Common.IO;

namespace UnifyBuild.Nuke;

/// <summary>
/// Foundation component: loads build.config.json and provides BuildContext.
/// All other components depend on this.
/// </summary>
public interface IUnifyBuildConfig : INukeBuild
{
    /// <summary>
    /// Resolved build configuration. Consumers can override resolution logic
    /// via explicit interface implementation.
    /// </summary>
    BuildContext UnifyConfig => TryGetValue(() => UnifyConfig)
        ?? BuildContextLoader.FromJson((AbsolutePath)RootDirectory, "build.config.json");

    /// <summary>
    /// Configuration to build - Default is 'Release'.
    /// </summary>
    [Parameter("Configuration to build - Default is 'Release'")]
    string Configuration => TryGetValue(() => Configuration) ?? "Release";
}
