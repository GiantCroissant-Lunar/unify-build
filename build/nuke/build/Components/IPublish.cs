using System;
using System.IO;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

interface IPublish : ICompile, IBuildConfig, IVersioning
{
    [Parameter("Runtime identifier for publishing")]
    string Runtime => TryGetValue(() => Runtime) ?? "win-x64";

    bool SelfContained => true;

    /// <summary>
    /// Generic publish target that publishes projects listed in Config.PublishProjectPaths
    /// to the versioned artifacts directory.
    /// </summary>
    Target Publish => _ => _
        .DependsOn<IRestore>()
        .AssuredAfterFailure()
        .Executes(() =>
        {
            // Ensure build-logs directory exists for this version before writing logs
            Directory.CreateDirectory(BuildLogsDirectory);

            var buildConfig = this as IBuildConfig;
            var configuredProjects = buildConfig?.Config?.PublishProjectPaths;

            if (configuredProjects == null || configuredProjects.Count == 0)
            {
                Console.WriteLine("No PublishProjectPaths configured in build.config.json; skipping publish.");
                return;
            }

            foreach (var relativePath in configuredProjects)
            {
                var projectPath = RootDirectory / relativePath;
                var projectName = Path.GetFileNameWithoutExtension(projectPath);

                var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ssZ");
                var metaFile = Path.Combine((string)BuildLogsDirectory, $"publish-{projectName}-{timestamp}.log");
                File.AppendAllText(metaFile,
                    $"Publish run at {DateTime.UtcNow:o}{Environment.NewLine}" +
                    $"Version: {ArtifactsVersion}{Environment.NewLine}" +
                    $"Runtime: {Runtime}{Environment.NewLine}" +
                    $"Configuration: {Configuration}{Environment.NewLine}" +
                    $"Project: {projectName}{Environment.NewLine}");

                try
                {
                    DotNetPublish(s => s
                        .SetProject(projectPath)
                        .SetConfiguration(Configuration)
                        .SetOutput(PublishDirectory / projectName)
                        .SetRuntime(Runtime)
                        .SetSelfContained(SelfContained));

                    File.AppendAllText(metaFile,
                        $"Status: Success{Environment.NewLine}{Environment.NewLine}");
                }
                catch (Exception ex)
                {
                    File.AppendAllText(metaFile,
                        $"Status: Failed{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
                    throw;
                }
            }
        });
}
