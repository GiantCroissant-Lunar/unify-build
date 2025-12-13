using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Utilities.Collections;

interface IClean : INukeBuild, IBuildConfig, IVersioning
{
    /// <summary>
    /// Root of source code to clean. Driven by Config.SourceDir (default "dotnet").
    /// </summary>
    AbsolutePath SourceDirectory => RootDirectory / Config.SourceDir;

    /// <summary>
    /// Root artifacts directory. Uses the shared ArtifactsRoot convention.
    /// </summary>
    AbsolutePath ArtifactsDirectory => ArtifactsRoot;

    Target Clean => _ => _
        .Before<IRestore>()
        .Executes(() =>
        {
            // Clean typical .NET build outputs under the configured source directory
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(d => d.DeleteDirectory());
            ArtifactsDirectory.CreateOrCleanDirectory();
        });
}
