using System;
using System.IO;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

interface ICompile : INukeBuild, IBuildConfig
{
    [Parameter("Configuration to build")]
    string Configuration => TryGetValue(() => Configuration) ?? "Debug";

    Target Compile => _ => _
        .DependsOn<IRestore>()
        .Executes(() =>
        {
            var cfg = Config;
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.SolutionPath))
            {
                Console.WriteLine("No SolutionPath configured in build.config.json; skipping compile.");
                return;
            }

            var solutionPath = RootDirectory / cfg.SolutionPath;
            if (!File.Exists(solutionPath))
            {
                Console.WriteLine($"Solution file not found at '{solutionPath}'; skipping compile.");
                return;
            }

            DotNetBuild(s => s
                .SetProjectFile(solutionPath)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });
}
