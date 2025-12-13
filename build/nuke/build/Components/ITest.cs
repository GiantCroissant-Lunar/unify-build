using System;
using System.IO;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

interface ITest : ICompile, IBuildConfig
{
    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var cfg = Config;
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.SolutionPath))
            {
                Console.WriteLine("No SolutionPath configured in build.config.json; skipping tests.");
                return;
            }

            var solutionPath = RootDirectory / cfg.SolutionPath;
            if (!File.Exists(solutionPath))
            {
                Console.WriteLine($"Solution file not found at '{solutionPath}'; skipping tests.");
                return;
            }

            DotNetTest(s => s
                .SetProjectFile(solutionPath)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoRestore());
        });
}
