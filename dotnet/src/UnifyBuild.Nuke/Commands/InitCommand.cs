using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Nuke.Common.IO;

namespace UnifyBuild.Nuke.Commands;

/// <summary>
/// Represents a discovered .csproj project with metadata extracted from the project file.
/// </summary>
public sealed record DiscoveredProject(
    string FullPath,
    string Name,
    string RelativePath,
    string ParentDirectory,
    string OutputType  // "Exe" or "Library"
);

/// <summary>
/// Options for the init command.
/// </summary>
public sealed record InitOptions(
    string OutputPath,
    bool Interactive,
    string? Template,  // "library" | "application" | null
    bool Force         // overwrite existing
);

/// <summary>
/// Result of the init command execution.
/// </summary>
public sealed record InitResult(
    string ConfigPath,
    BuildJsonConfig GeneratedConfig,
    List<string> DiscoveredProjects
);

/// <summary>
/// Scaffolds a valid build.config.json by discovering projects in the repository
/// and generating configuration from templates or discovered project metadata.
/// </summary>
public sealed class InitCommand
{
    private static readonly string[] ExcludedDirectories = { "bin", "obj", ".git", "node_modules" };

    private const string SchemaReference = "./build.config.schema.json";

    /// <summary>
    /// Executes the init command: discovers projects, generates config, and writes to disk.
    /// When Interactive mode is enabled, launches the ConfigWizard for step-by-step configuration.
    /// </summary>
    public InitResult Execute(AbsolutePath repoRoot, InitOptions options)
    {
        var configPath = Path.Combine(options.OutputPath, "build.config.json");

        if (File.Exists(configPath) && !options.Force)
        {
            throw new InvalidOperationException(
                $"Config file already exists at '{configPath}'. Use --force to overwrite.");
        }

        BuildJsonConfig config;
        List<string> discoveredPaths;

        if (options.Interactive)
        {
            // Run the interactive wizard
            var wizard = new ConfigWizard();
            var (wizardConfig, wizardPaths) = wizard.Run(repoRoot);
            config = wizardConfig;
            discoveredPaths = wizardPaths;
        }
        else if (!string.IsNullOrEmpty(options.Template))
        {
            var discoveredProjects = DiscoverProjects(repoRoot);
            discoveredPaths = discoveredProjects.Select(p => p.RelativePath).ToList();
            config = GenerateFromTemplate(options.Template, discoveredProjects);
        }
        else
        {
            var discoveredProjects = DiscoverProjects(repoRoot);
            discoveredPaths = discoveredProjects.Select(p => p.RelativePath).ToList();
            config = GenerateFromDiscovery(discoveredProjects);
        }

        var serialized = SerializeConfig(config);
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, serialized, Encoding.UTF8);

        return new InitResult(configPath, config, discoveredPaths);
    }

    /// <summary>
    /// Recursively discovers .csproj files from the repository root,
    /// excluding bin/, obj/, .git/, and node_modules/ directories.
    /// </summary>
    internal List<DiscoveredProject> DiscoverProjects(AbsolutePath repoRoot)
    {
        var projects = new List<DiscoveredProject>();
        DiscoverProjectsRecursive(repoRoot, repoRoot, projects);
        return projects;
    }

    private void DiscoverProjectsRecursive(
        AbsolutePath repoRoot,
        AbsolutePath currentDir,
        List<DiscoveredProject> results)
    {
        // Find .csproj files in the current directory
        if (Directory.Exists(currentDir))
        {
            foreach (var file in Directory.GetFiles(currentDir, "*.csproj"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var relativePath = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
                var parentDir = Path.GetRelativePath(repoRoot, Path.GetDirectoryName(file)!).Replace('\\', '/');
                var outputType = DetectOutputType(file);

                results.Add(new DiscoveredProject(
                    FullPath: file,
                    Name: name,
                    RelativePath: relativePath,
                    ParentDirectory: parentDir,
                    OutputType: outputType
                ));
            }

            // Recurse into subdirectories, skipping excluded ones
            foreach (var subDir in Directory.GetDirectories(currentDir))
            {
                var dirName = Path.GetFileName(subDir);
                if (!IsExcludedDirectory(dirName))
                {
                    DiscoverProjectsRecursive(repoRoot, (AbsolutePath)subDir, results);
                }
            }
        }
    }

    private static bool IsExcludedDirectory(string directoryName)
    {
        return ExcludedDirectories.Any(excluded =>
            directoryName.Equals(excluded, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Detects the output type of a .csproj file by parsing the OutputType element.
    /// Returns "Exe" for executables, "Library" for everything else.
    /// </summary>
    internal static string DetectOutputType(string csprojPath)
    {
        try
        {
            var doc = XDocument.Load(csprojPath);
            var outputType = doc.Descendants("OutputType").FirstOrDefault()?.Value;
            if (outputType is not null &&
                outputType.Equals("Exe", StringComparison.OrdinalIgnoreCase))
            {
                return "Exe";
            }
        }
        catch
        {
            // If we can't parse the csproj, default to Library
        }

        return "Library";
    }

    /// <summary>
    /// Generates a BuildJsonConfig from a template name ("library" or "application")
    /// using the discovered projects.
    /// </summary>
    internal BuildJsonConfig GenerateFromTemplate(string template, List<DiscoveredProject> projects)
    {
        return template.ToLowerInvariant() switch
        {
            "library" => GenerateLibraryTemplate(projects),
            "application" => GenerateApplicationTemplate(projects),
            _ => throw new ArgumentException(
                $"Unknown template '{template}'. Supported templates: 'library', 'application'.")
        };
    }

    private static BuildJsonConfig GenerateLibraryTemplate(List<DiscoveredProject> projects)
    {
        var groups = new Dictionary<string, ProjectGroup>();

        // Group all library projects under a "packages" group with pack action
        var libraryProjects = projects.Where(p => p.OutputType == "Library").ToList();
        if (libraryProjects.Count > 0)
        {
            var commonSourceDir = FindCommonSourceDir(libraryProjects);
            groups["packages"] = new ProjectGroup
            {
                SourceDir = commonSourceDir,
                Action = "pack",
                Include = libraryProjects.Select(p => p.Name).ToArray()
            };
        }

        return new BuildJsonConfig
        {
            ProjectGroups = groups.Count > 0 ? groups : null,
            PackIncludeSymbols = true
        };
    }

    private static BuildJsonConfig GenerateApplicationTemplate(List<DiscoveredProject> projects)
    {
        var groups = new Dictionary<string, ProjectGroup>();

        // Group executable projects under "apps" with publish action
        var exeProjects = projects.Where(p => p.OutputType == "Exe").ToList();
        if (exeProjects.Count > 0)
        {
            var commonSourceDir = FindCommonSourceDir(exeProjects);
            groups["apps"] = new ProjectGroup
            {
                SourceDir = commonSourceDir,
                Action = "publish",
                Include = exeProjects.Select(p => p.Name).ToArray()
            };
        }

        // Group library projects under "libraries" with compile action
        var libProjects = projects.Where(p => p.OutputType == "Library").ToList();
        if (libProjects.Count > 0)
        {
            var commonSourceDir = FindCommonSourceDir(libProjects);
            groups["libraries"] = new ProjectGroup
            {
                SourceDir = commonSourceDir,
                Action = "compile",
                Include = libProjects.Select(p => p.Name).ToArray()
            };
        }

        // Detect solution file
        string? solutionPath = null;
        if (exeProjects.Count > 0)
        {
            var firstProject = exeProjects[0];
            var projectDir = Path.GetDirectoryName(firstProject.FullPath);
            if (projectDir is not null)
            {
                // Walk up looking for a .sln file
                var dir = new DirectoryInfo(projectDir);
                while (dir is not null)
                {
                    var slnFiles = dir.GetFiles("*.sln");
                    if (slnFiles.Length > 0)
                    {
                        solutionPath = slnFiles[0].Name;
                        break;
                    }
                    dir = dir.Parent;
                }
            }
        }

        return new BuildJsonConfig
        {
            Solution = solutionPath,
            ProjectGroups = groups.Count > 0 ? groups : null
        };
    }

    /// <summary>
    /// Generates a BuildJsonConfig from discovered projects by grouping them
    /// by parent directory and detecting appropriate build actions.
    /// </summary>
    private static BuildJsonConfig GenerateFromDiscovery(List<DiscoveredProject> projects)
    {
        var groups = new Dictionary<string, ProjectGroup>();

        // Group projects by their grandparent directory (e.g., "src/apps", "src/libs")
        var grouped = projects
            .GroupBy(p => GetGroupDirectory(p.ParentDirectory))
            .ToList();

        foreach (var group in grouped)
        {
            var groupProjects = group.ToList();
            var groupName = SanitizeGroupName(group.Key);

            // Determine action based on output types in the group
            var hasExe = groupProjects.Any(p => p.OutputType == "Exe");
            var action = hasExe ? "publish" : "pack";

            var commonSourceDir = FindCommonSourceDir(groupProjects);

            groups[groupName] = new ProjectGroup
            {
                SourceDir = commonSourceDir,
                Action = action,
                Include = groupProjects.Select(p => p.Name).ToArray()
            };
        }

        return new BuildJsonConfig
        {
            ProjectGroups = groups.Count > 0 ? groups : null
        };
    }

    /// <summary>
    /// Serializes a BuildJsonConfig to JSON with $schema reference and explanatory comments.
    /// </summary>
    internal string SerializeConfig(BuildJsonConfig config)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(config, options);

        // Parse and rebuild with $schema at the top
        using var doc = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();

            // Write $schema first
            writer.WriteString("$schema", SchemaReference);

            // Write all other properties from the serialized config
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                property.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        var jsonWithSchema = Encoding.UTF8.GetString(stream.ToArray());

        // Add comments explaining each section
        return AddConfigComments(jsonWithSchema);
    }

    private static string AddConfigComments(string json)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// UnifyBuild configuration file");
        sb.AppendLine("// See https://github.com/nicepkg/UnifyBuild/blob/main/docs/configuration-reference.md");
        sb.AppendLine(json);
        return sb.ToString();
    }

    /// <summary>
    /// Finds the common source directory for a set of projects.
    /// Returns the shortest common parent path relative to repo root.
    /// </summary>
    private static string FindCommonSourceDir(List<DiscoveredProject> projects)
    {
        if (projects.Count == 0)
            return "src";

        if (projects.Count == 1)
            return projects[0].ParentDirectory;

        // Find common prefix of parent directories
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
            else
            {
                break;
            }
        }

        return commonParts.Count > 0 ? string.Join("/", commonParts) : "src";
    }

    /// <summary>
    /// Gets the group directory from a project's parent directory.
    /// Uses the first two path segments (e.g., "src/apps" from "src/apps/MyApp").
    /// Falls back to the full parent directory if it has fewer segments.
    /// </summary>
    private static string GetGroupDirectory(string parentDirectory)
    {
        var parts = parentDirectory.Split('/');
        if (parts.Length >= 2)
            return $"{parts[0]}/{parts[1]}";
        return parentDirectory;
    }

    /// <summary>
    /// Sanitizes a directory path into a valid group name.
    /// </summary>
    private static string SanitizeGroupName(string directoryPath)
    {
        // Use the last segment of the path as the group name
        var parts = directoryPath.Split('/');
        var name = parts[^1];

        // Replace non-alphanumeric characters with hyphens
        return new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray())
            .Trim('-')
            .ToLowerInvariant();
    }
}
