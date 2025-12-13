using System;
using System.IO;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

interface IRestore : INukeBuild, IBuildConfig
{
    Target Restore => _ => _
        .Executes(() =>
        {
            var cfg = Config;
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.SolutionPath))
            {
                Console.WriteLine("No SolutionPath configured in build.config.json; skipping restore.");
                return;
            }

            var solutionPath = RootDirectory / cfg.SolutionPath;
            if (!File.Exists(solutionPath))
            {
                Console.WriteLine($"Solution file not found at '{solutionPath}'; skipping restore.");
                return;
            }

            DotNetRestore(s => s
                .SetProjectFile(solutionPath));
        });
}
