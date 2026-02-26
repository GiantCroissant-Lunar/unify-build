using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nuke.Common.IO;
using Spectre.Console;

namespace UnifyBuild.Nuke.Commands;

/// <summary>
/// Represents a detected repository technology/file type beyond .csproj.
/// </summary>
public sealed record DetectedTechnology(
    string Name,
    string Description,
    string DetectedPath
);

/// <summary>
/// Interactive configuration wizard using Spectre.Console for multi-step
/// build.config.json generation. Falls back to auto-generation when
/// running in a non-interactive terminal.
/// </summary>
public sealed class ConfigWizard
{
    private readonly InitCommand _initCommand = new();

    /// <summary>
    /// Runs the interactive wizard and returns the generated config and discovered project paths.
    /// Falls back to auto-generation if the terminal is not interactive.
    /// </summary>
    public (BuildJsonConfig Config, List<string> DiscoveredPaths) Run(AbsolutePath repoRoot)
    {
        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            return RunFallback(repoRoot);
        }

        AnsiConsole.Write(new Rule("[bold blue]UnifyBuild Configuration Wizard[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        // Step 1: Detect repository structure
        var projects = _initCommand.DiscoverProjects(repoRoot);
        var technologies = DetectTechnologies(repoRoot);

        DisplayDetectionResults(projects, technologies);

        if (projects.Count == 0 && technologies.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No projects or supported technologies detected.[/]");
            AnsiConsole.MarkupLine("Generating a minimal configuration...");
            var minimalConfig = new BuildJsonConfig();
            return (minimalConfig, new List<string>());
        }

        // Step 2: Multi-select project picker
        var selectedProjects = PickProjects(projects);

        // Step 3: Build action selection per group
        var projectGroups = BuildProjectGroups(selectedProjects);

        // Step 4: Native build config detection
        var nativeBuild = ConfigureNativeBuild(repoRoot, technologies);

        // Step 5: Rust build config detection
        var rustBuild = ConfigureRustBuild(repoRoot, technologies);

        // Step 6: Go build config detection
        var goBuild = ConfigureGoBuild(repoRoot, technologies);

        // Step 7: Unity config detection
        var unityBuild = ConfigureUnityBuild(repoRoot, technologies);

        // Assemble config
        var config = new BuildJsonConfig
        {
            ProjectGroups = projectGroups.Count > 0 ? projectGroups : null,
            NativeBuild = nativeBuild,
            RustBuild = rustBuild,
            GoBuild = goBuild,
            UnityBuild = unityBuild
        };

        // Step 8: Preview and confirm
        var confirmed = PreviewAndConfirm(config);
        if (!confirmed)
        {
            AnsiConsole.MarkupLine("[yellow]Configuration cancelled.[/]");
            throw new OperationCanceledException("Wizard cancelled by user.");
        }

        var discoveredPaths = selectedProjects.Select(p => p.RelativePath).ToList();
        return (config, discoveredPaths);
    }

    /// <summary>
    /// Fallback for non-interactive terminals: uses auto-generation from InitCommand.
    /// </summary>
    private (BuildJsonConfig Config, List<string> DiscoveredPaths) RunFallback(AbsolutePath repoRoot)
    {
        var projects = _initCommand.DiscoverProjects(repoRoot);
        var config = projects.Count > 0
            ? _initCommand.GenerateFromTemplate("library", projects)
            : new BuildJsonConfig();
        var paths = projects.Select(p => p.RelativePath).ToList();
        return (config, paths);
    }

    /// <summary>
    /// Detects non-.NET technologies in the repository (CMake, Rust, Go, Unity).
    /// </summary>
    internal static List<DetectedTechnology> DetectTechnologies(AbsolutePath repoRoot)
    {
        var technologies = new List<DetectedTechnology>();

        // CMake detection
        var cmakePaths = FindFiles(repoRoot, "CMakeLists.txt");
        if (cmakePaths.Count > 0)
        {
            technologies.Add(new DetectedTechnology(
                "CMake",
                "C/C++ build system (CMakeLists.txt found)",
                cmakePaths[0]
            ));
        }

        // Rust detection
        var cargoPaths = FindFiles(repoRoot, "Cargo.toml");
        if (cargoPaths.Count > 0)
        {
            technologies.Add(new DetectedTechnology(
                "Rust",
                "Rust project (Cargo.toml found)",
                cargoPaths[0]
            ));
        }

        // Go detection
        var goModPaths = FindFiles(repoRoot, "go.mod");
        if (goModPaths.Count > 0)
        {
            technologies.Add(new DetectedTechnology(
                "Go",
                "Go module (go.mod found)",
                goModPaths[0]
            ));
        }

        // Unity detection
        if (Directory.Exists(Path.Combine(repoRoot, "Assets")) &&
            Directory.Exists(Path.Combine(repoRoot, "ProjectSettings")))
        {
            technologies.Add(new DetectedTechnology(
                "Unity",
                "Unity project (Assets/ and ProjectSettings/ found)",
                Path.Combine(repoRoot, "Assets")
            ));
        }
        else
        {
            // Check subdirectories for Unity projects
            foreach (var dir in SafeGetDirectories(repoRoot))
            {
                var dirName = Path.GetFileName(dir);
                if (IsExcludedScanDirectory(dirName)) continue;

                if (Directory.Exists(Path.Combine(dir, "Assets")) &&
                    Directory.Exists(Path.Combine(dir, "ProjectSettings")))
                {
                    technologies.Add(new DetectedTechnology(
                        "Unity",
                        "Unity project (Assets/ and ProjectSettings/ found)",
                        Path.Combine(dir, "Assets")
                    ));
                    break;
                }
            }
        }

        return technologies;
    }

    private static List<string> FindFiles(AbsolutePath repoRoot, string fileName)
    {
        var results = new List<string>();
        FindFilesRecursive(repoRoot, fileName, results, maxDepth: 3, currentDepth: 0);
        return results;
    }

    private static void FindFilesRecursive(string dir, string fileName, List<string> results, int maxDepth, int currentDepth)
    {
        if (currentDepth > maxDepth) return;

        var filePath = Path.Combine(dir, fileName);
        if (File.Exists(filePath))
        {
            results.Add(filePath);
        }

        foreach (var subDir in SafeGetDirectories(dir))
        {
            var dirName = Path.GetFileName(subDir);
            if (!IsExcludedScanDirectory(dirName))
            {
                FindFilesRecursive(subDir, fileName, results, maxDepth, currentDepth + 1);
            }
        }
    }

    private static string[] SafeGetDirectories(string path)
    {
        try { return Directory.GetDirectories(path); }
        catch { return Array.Empty<string>(); }
    }

    private static bool IsExcludedScanDirectory(string dirName)
    {
        return dirName is "bin" or "obj" or ".git" or "node_modules" or ".vs" or ".idea" or "packages";
    }

    private static void DisplayDetectionResults(List<DiscoveredProject> projects, List<DetectedTechnology> technologies)
    {
        AnsiConsole.MarkupLine($"[green]Detected {projects.Count} .NET project(s)[/]");

        if (technologies.Count > 0)
        {
            foreach (var tech in technologies)
            {
                AnsiConsole.MarkupLine($"[green]Detected {tech.Name}:[/] {tech.Description}");
            }
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Step 2: Multi-select project picker using Spectre.Console.
    /// </summary>
    private static List<DiscoveredProject> PickProjects(List<DiscoveredProject> projects)
    {
        if (projects.Count == 0)
            return new List<DiscoveredProject>();

        AnsiConsole.MarkupLine("[bold]Step 1:[/] Select projects to include in your build configuration");
        AnsiConsole.MarkupLine("[dim]Use space to toggle, enter to confirm[/]");
        AnsiConsole.WriteLine();

        var prompt = new MultiSelectionPrompt<string>()
            .Title("Which projects should be included?")
            .PageSize(15)
            .MoreChoicesText("[grey](Move up and down to see more projects)[/]")
            .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to accept)[/]");

        foreach (var project in projects)
        {
            var label = $"{project.Name} ({project.OutputType}) - {project.RelativePath}";
            prompt.AddChoice(label);
        }

        // Pre-select all by default
        var allLabels = projects.Select(p => $"{p.Name} ({p.OutputType}) - {p.RelativePath}").ToArray();
        prompt.AddChoices(Array.Empty<string>()); // no-op, choices already added
        foreach (var label in allLabels)
        {
            prompt.Select(label);
        }

        var selectedLabels = AnsiConsole.Prompt(prompt);

        return projects
            .Where(p => selectedLabels.Contains($"{p.Name} ({p.OutputType}) - {p.RelativePath}"))
            .ToList();
    }

    /// <summary>
    /// Step 3: Group selected projects and let user choose build action per group.
    /// </summary>
    private static Dictionary<string, ProjectGroup> BuildProjectGroups(List<DiscoveredProject> selectedProjects)
    {
        if (selectedProjects.Count == 0)
            return new Dictionary<string, ProjectGroup>();

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Step 2:[/] Configure build actions for project groups");
        AnsiConsole.MarkupLine("[dim]Projects are grouped by directory. Choose a build action for each group.[/]");
        AnsiConsole.WriteLine();

        var groups = new Dictionary<string, ProjectGroup>();

        // Group by parent directory
        var grouped = selectedProjects
            .GroupBy(p => GetGroupKey(p.ParentDirectory))
            .ToList();

        foreach (var group in grouped)
        {
            var groupProjects = group.ToList();
            var groupName = SanitizeGroupName(group.Key);
            var projectNames = string.Join(", ", groupProjects.Select(p => p.Name));

            // Suggest action based on output types
            var hasExe = groupProjects.Any(p => p.OutputType == "Exe");
            var suggestedAction = hasExe ? "publish" : "pack";

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"Build action for [blue]{groupName}[/] ({projectNames}):")
                    .AddChoices("compile", "pack", "publish")
                    .HighlightStyle(new Style(Color.Blue))
            );

            // Determine common source directory
            var commonSourceDir = FindCommonSourceDir(groupProjects);

            groups[groupName] = new ProjectGroup
            {
                SourceDir = commonSourceDir,
                Action = action,
                Include = groupProjects.Select(p => p.Name).ToArray()
            };
        }

        return groups;
    }

    private static string GetGroupKey(string parentDirectory)
    {
        var parts = parentDirectory.Split('/');
        if (parts.Length >= 2)
            return $"{parts[0]}/{parts[1]}";
        return parentDirectory;
    }

    private static string SanitizeGroupName(string directoryPath)
    {
        var parts = directoryPath.Split('/');
        var name = parts[^1];
        return new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray())
            .Trim('-')
            .ToLowerInvariant();
    }

    private static string FindCommonSourceDir(List<DiscoveredProject> projects)
    {
        if (projects.Count == 0) return "src";
        if (projects.Count == 1) return projects[0].ParentDirectory;

        var dirs = projects.Select(p => p.ParentDirectory).ToList();
        var parts = dirs[0].Split('/');
        var commonParts = new List<string>();

        for (int i = 0; i < parts.Length; i++)
        {
            if (dirs.All(d =>
            {
                var dParts = d.Split('/');
                return i < dParts.Length &&
                       dParts[i].Equals(parts[i], StringComparison.OrdinalIgnoreCase);
            }))
            {
                commonParts.Add(parts[i]);
            }
            else break;
        }

        return commonParts.Count > 0 ? string.Join("/", commonParts) : "src";
    }

    /// <summary>
    /// Step 4: Configure native (CMake) build if detected.
    /// </summary>
    private static NativeBuildConfig? ConfigureNativeBuild(AbsolutePath repoRoot, List<DetectedTechnology> technologies)
    {
        var cmake = technologies.FirstOrDefault(t => t.Name == "CMake");
        if (cmake is null) return null;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Step 3:[/] Native Build Configuration");
        AnsiConsole.MarkupLine($"[dim]CMakeLists.txt detected at: {Path.GetRelativePath(repoRoot, cmake.DetectedPath)}[/]");

        var include = AnsiConsole.Confirm("Include CMake native build in configuration?", defaultValue: true);
        if (!include) return null;

        var sourceDir = Path.GetRelativePath(repoRoot, Path.GetDirectoryName(cmake.DetectedPath)!).Replace('\\', '/');

        return new NativeBuildConfig
        {
            Enabled = true,
            CMakeSourceDir = sourceDir,
            CMakeBuildDir = $"{sourceDir}/build",
            BuildConfig = "Release"
        };
    }

    /// <summary>
    /// Step 4b: Configure Rust build if detected.
    /// </summary>
    private static RustBuildConfig? ConfigureRustBuild(AbsolutePath repoRoot, List<DetectedTechnology> technologies)
    {
        var rust = technologies.FirstOrDefault(t => t.Name == "Rust");
        if (rust is null) return null;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Rust Build Configuration[/]");
        AnsiConsole.MarkupLine($"[dim]Cargo.toml detected at: {Path.GetRelativePath(repoRoot, rust.DetectedPath)}[/]");

        var include = AnsiConsole.Confirm("Include Rust build in configuration?", defaultValue: true);
        if (!include) return null;

        var manifestDir = Path.GetRelativePath(repoRoot, Path.GetDirectoryName(rust.DetectedPath)!).Replace('\\', '/');

        var profile = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Cargo build profile:")
                .AddChoices("release", "debug")
        );

        return new RustBuildConfig
        {
            Enabled = true,
            CargoManifestDir = manifestDir == "." ? null : manifestDir,
            Profile = profile
        };
    }

    /// <summary>
    /// Step 4c: Configure Go build if detected.
    /// </summary>
    private static GoBuildConfig? ConfigureGoBuild(AbsolutePath repoRoot, List<DetectedTechnology> technologies)
    {
        var go = technologies.FirstOrDefault(t => t.Name == "Go");
        if (go is null) return null;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Go Build Configuration[/]");
        AnsiConsole.MarkupLine($"[dim]go.mod detected at: {Path.GetRelativePath(repoRoot, go.DetectedPath)}[/]");

        var include = AnsiConsole.Confirm("Include Go build in configuration?", defaultValue: true);
        if (!include) return null;

        var moduleDir = Path.GetRelativePath(repoRoot, Path.GetDirectoryName(go.DetectedPath)!).Replace('\\', '/');

        return new GoBuildConfig
        {
            Enabled = true,
            GoModuleDir = moduleDir == "." ? null : moduleDir
        };
    }

    /// <summary>
    /// Step 5: Configure Unity build if detected.
    /// </summary>
    private static UnityBuildJsonConfig? ConfigureUnityBuild(AbsolutePath repoRoot, List<DetectedTechnology> technologies)
    {
        var unity = technologies.FirstOrDefault(t => t.Name == "Unity");
        if (unity is null) return null;

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Unity Build Configuration[/]");
        AnsiConsole.MarkupLine($"[dim]Unity project detected at: {Path.GetRelativePath(repoRoot, unity.DetectedPath)}[/]");

        var include = AnsiConsole.Confirm("Include Unity build in configuration?", defaultValue: true);
        if (!include) return null;

        var unityRoot = Path.GetRelativePath(repoRoot, Path.GetDirectoryName(unity.DetectedPath)!).Replace('\\', '/');

        return new UnityBuildJsonConfig
        {
            UnityProjectRoot = unityRoot == "." ? "" : unityRoot,
            TargetFramework = "netstandard2.1"
        };
    }

    /// <summary>
    /// Step 6-7: Show config preview and confirm save.
    /// </summary>
    private bool PreviewAndConfirm(BuildJsonConfig config)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Configuration Preview[/]");
        AnsiConsole.WriteLine();

        var json = _initCommand.SerializeConfig(config);

        var panel = new Panel(
            new Text(json).Overflow(Overflow.Fold))
        {
            Header = new PanelHeader("[blue]build.config.json[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        // Inline validation summary
        DisplayValidationSummary(config);

        return AnsiConsole.Confirm("Save this configuration?", defaultValue: true);
    }

    /// <summary>
    /// Displays a quick validation summary of the generated config.
    /// </summary>
    private static void DisplayValidationSummary(BuildJsonConfig config)
    {
        var issues = new List<string>();

        if (config.ProjectGroups is null || config.ProjectGroups.Count == 0)
        {
            issues.Add("[yellow]Warning:[/] No project groups defined");
        }
        else
        {
            foreach (var (name, group) in config.ProjectGroups)
            {
                if (string.IsNullOrEmpty(group.SourceDir))
                    issues.Add($"[yellow]Warning:[/] Group '{name}' has no source directory");

                if (group.Include is null || group.Include.Length == 0)
                    issues.Add($"[yellow]Warning:[/] Group '{name}' has no included projects");
            }
        }

        if (issues.Count > 0)
        {
            foreach (var issue in issues)
                AnsiConsole.MarkupLine(issue);
            AnsiConsole.WriteLine();
        }
        else
        {
            AnsiConsole.MarkupLine("[green]Configuration looks good![/]");
            AnsiConsole.WriteLine();
        }
    }
}
