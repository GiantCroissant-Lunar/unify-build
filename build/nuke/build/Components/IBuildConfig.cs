using System;
using System.Collections.Generic;
using System.IO;
using Nuke.Common;
using Nuke.Common.IO;

// Lightweight build configuration component to avoid hard-coded paths in Build.cs
// Reads an optional JSON file at build/nuke/build.config.json. Falls back to sensible defaults.
// Example JSON:
// {
//   "solutionPath": "dotnet/MySolution.sln",
//   "sourceDir": "dotnet",
//   "websiteDir": "website",
//   "frameworkDirs": ["framework"],
//   "pluginDirs": ["plugins"],
//   "packPlugins": true,
//   "packFramework": true,
//   "excludePluginNames": [],
//   "includePluginNames": [],
//   "publishProjectPaths": [],
//   "packProjectPaths": []
// }

interface IBuildConfig : INukeBuild
{
    AbsolutePath BuildConfigPath => RootDirectory / "build" / "nuke" / "build.config.json";

    BuildConfig Config => BuildConfig.Load(BuildConfigPath);
}

class BuildConfig
{
    // Optional path to the primary solution relative to the repository root, e.g. "dotnet/MySolution.sln".
    public string SolutionPath { get; set; } = null;

    public string SourceDir { get; set; } = "dotnet";
    public string WebsiteDir { get; set; } = "website";
    public List<string> FrameworkDirs { get; set; } = new() { "framework" };
    public List<string> PluginDirs { get; set; } = new() { "plugins" };
    public bool PackPlugins { get; set; } = true;
    public bool PackFramework { get; set; } = true;
    public List<string> ExcludePluginNames { get; set; } = new();
    public List<string> IncludePluginNames { get; set; } = new();
    public List<string> PublishProjectPaths { get; set; } = new();
    public List<string> PackProjectPaths { get; set; } = new();

    // Local NuGet feed sync settings
    public bool SyncLocalNugetFeed { get; set; } = false;
    public string LocalNugetFeedRoot { get; set; } = null; // e.g., D:\\lunar-snake\\packages\\nuget-repo
    public string LocalNugetFeedFlatSubdir { get; set; } = "flat";
    public string LocalNugetFeedHierarchicalSubdir { get; set; } = "hierarchical";
    public string LocalNugetFeedBaseUrl { get; set; } = null; // Optional: base URL if served over HTTP for V3 index

    public static BuildConfig Load(AbsolutePath configPath)
    {
        try
        {
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var cfg = System.Text.Json.JsonSerializer.Deserialize<BuildConfig>(json);
                return MergeDefaults(cfg ?? new BuildConfig());
            }
        }
        catch (Exception)
        {
            // Fall back to defaults on any error
        }

        return new BuildConfig();
    }

    private static BuildConfig MergeDefaults(BuildConfig cfg)
    {
        cfg.SourceDir = string.IsNullOrWhiteSpace(cfg.SourceDir) ? "dotnet" : cfg.SourceDir;
        cfg.WebsiteDir = string.IsNullOrWhiteSpace(cfg.WebsiteDir) ? "website" : cfg.WebsiteDir;
        cfg.FrameworkDirs = (cfg.FrameworkDirs == null || cfg.FrameworkDirs.Count == 0) ? new() { "framework" } : cfg.FrameworkDirs;
        cfg.PluginDirs = (cfg.PluginDirs == null || cfg.PluginDirs.Count == 0) ? new() { "plugins" } : cfg.PluginDirs;
        cfg.ExcludePluginNames ??= new();
        cfg.IncludePluginNames ??= new();
        cfg.PublishProjectPaths ??= new();
        cfg.PackProjectPaths ??= new();
        return cfg;
    }
}
