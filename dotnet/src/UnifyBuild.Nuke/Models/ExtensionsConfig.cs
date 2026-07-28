namespace UnifyBuild.Nuke;

/// <summary>
/// Configuration for loading custom component plugins from external assemblies.
/// </summary>
public sealed class ExtensionsConfig
{
    /// <summary>
    /// Paths to plugin assemblies (.dll) or directories containing plugin assemblies.
    /// Paths are relative to the repository root.
    /// </summary>
    public string[]? PluginPaths { get; set; }

    /// <summary>
    /// Whether to automatically discover and load plugins from the default plugin directory (build/plugins/).
    /// Default: false.
    /// </summary>
    public bool AutoLoadPlugins { get; set; } = false;
}
