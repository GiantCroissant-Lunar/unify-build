using System;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using UnifyBuild.Nuke;

namespace UnifyBuild.Tool;

/// <summary>
/// Entry point for the unify-build CLI tool.
/// Composes all UnifyBuild components into a single NukeBuild class.
/// </summary>
class Build : NukeBuild, IUnify, IUnifyNative, IUnifyUnity
{
    BuildContext IUnifyBuildConfig.UnifyConfig =>
        BuildContextLoader.FromJson(RootDirectory, "build.config.json");

    public static int Main()
    {
        AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", true);
        
        // Find and set root directory before Nuke initializes
        var rootDirectory = FindBuildConfigDirectory();
        Environment.SetEnvironmentVariable("NUKE_ROOT_DIRECTORY", rootDirectory);
        
        return Execute<Build>();
    }

    /// <summary>
    /// Walk up from current directory looking for build.config.json or build/build.config.json.
    /// </summary>
    private static string FindBuildConfigDirectory()
    {
        var current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            // Check for build.config.json in current directory
            if (File.Exists(Path.Combine(current, "build.config.json")))
            {
                return current;
            }

            // Check for build/build.config.json pattern
            var buildSubdir = Path.Combine(current, "build");
            if (File.Exists(Path.Combine(buildSubdir, "build.config.json")))
            {
                return current;
            }

            var parent = Directory.GetParent(current);
            current = parent?.FullName;
        }

        throw new InvalidOperationException(
            "Could not find build.config.json in current directory or any parent. " +
            "Ensure you are running from within a repository with a build.config.json file.");
    }
}
