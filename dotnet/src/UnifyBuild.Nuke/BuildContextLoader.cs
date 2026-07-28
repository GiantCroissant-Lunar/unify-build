using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nuke.Common.IO;

namespace UnifyBuild.Nuke;

/// <summary>
/// Loader for build configuration using the generic project groups schema.
/// </summary>
public static class BuildContextLoader
{
    /// <summary>
    /// Load build configuration from JSON file.
    /// </summary>
    /// <param name="repoRoot">Repository root directory</param>
    /// <param name="configFile">Config file name (default: "build.config.json")</param>
    /// <returns>BuildContext representing the configuration</returns>
    public static BuildContext FromJson(AbsolutePath repoRoot, string configFile = "build.config.json")
        => FromJson(repoRoot, configFile, null);

    /// <summary>
    /// Load build configuration from JSON file with external version.
    /// </summary>
    /// <param name="repoRoot">Repository root directory</param>
    /// <param name="configFile">Config file name</param>
    /// <param name="externalVersion">Version from external source (e.g., GitVersion)</param>
    /// <returns>BuildContext representing the configuration</returns>
    public static BuildContext FromJson(AbsolutePath repoRoot, string configFile, string? externalVersion)
    {
        // Use the shared resolver so build targets and validation share the same discovery rules.
        var path = BuildConfigResolver.ResolveConfigPath(repoRoot, configFile);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Build config file '{path}' not found.");

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        if (!json.Contains("\"projectGroups\"", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Build config must use 'projectGroups' schema. "
                + "See docs/rfcs/rfc-0001-generic-build-schema.md for schema documentation.");
        }

        // Check for deprecated v1 properties and log warnings
        CheckForDeprecatedProperties(json);

        global::Serilog.Log.Information("Loading build configuration");
        return LoadConfig(repoRoot, json, options, externalVersion);
    }

    /// <summary>
    /// Checks the raw JSON for deprecated v1 property names and logs warnings with migration guidance.
    /// </summary>
    private static void CheckForDeprecatedProperties(string json)
    {
        var deprecatedMappings = new (string Property, string Guidance)[]
        {
            ("hostsDir", "Use projectGroups with action 'publish' instead. Run 'dotnet unify-build migrate' to auto-migrate."),
            ("pluginsDir", "Use projectGroups with action 'pack' instead. Run 'dotnet unify-build migrate' to auto-migrate."),
            ("contractsDir", "Use projectGroups with action 'pack' instead. Run 'dotnet unify-build migrate' to auto-migrate."),
            ("includeHosts", "Use projectGroups[].include instead. Run 'dotnet unify-build migrate' to auto-migrate."),
            ("excludeHosts", "Use projectGroups[].exclude instead. Run 'dotnet unify-build migrate' to auto-migrate."),
            ("includePlugins", "Use projectGroups[].include instead. Run 'dotnet unify-build migrate' to auto-migrate."),
            ("excludePlugins", "Use projectGroups[].exclude instead. Run 'dotnet unify-build migrate' to auto-migrate."),
            ("includeContracts", "Use projectGroups[].include instead. Run 'dotnet unify-build migrate' to auto-migrate."),
            ("excludeContracts", "Use projectGroups[].exclude instead. Run 'dotnet unify-build migrate' to auto-migrate.")
        };

        foreach (var (property, guidance) in deprecatedMappings)
        {
            // Check for the property name in JSON (both camelCase and PascalCase)
            if (json.Contains($"\"{property}\"", StringComparison.OrdinalIgnoreCase))
            {
                global::Serilog.Log.Warning(
                    "[DEPRECATED] Property '{Property}' is deprecated. {Guidance}",
                    property, guidance);
            }
        }
    }

    private static BuildContext LoadConfig(AbsolutePath repoRoot, string json, JsonSerializerOptions options, string? externalVersion = null)
    {
        var cfg = JsonSerializer.Deserialize<BuildJsonConfig>(json, options)
                  ?? throw new InvalidOperationException("Failed to parse build config.");

        var version = cfg.Version
                      ?? GetEnv(cfg.VersionEnv)
                      ?? externalVersion
                      ?? GetEnv("GITVERSION_MAJORMINORPATCH")
                      ?? cfg.ArtifactsVersion
                      ?? TryDeriveVersionFromGit(repoRoot);

        if (version is null)
        {
            global::Serilog.Log.Warning(
                "[unify-build] No version source resolved (config 'version', env '{VersionEnv}', " +
                "env 'GITVERSION_MAJORMINORPATCH', config 'artifactsVersion', or git) — defaulting to 0.1.0. " +
                "Tag the repository or set the version env to stamp a real SemVer.",
                cfg.VersionEnv ?? "Version");
            version = "0.1.0";
        }

        string artifactsVersion = cfg.ArtifactsVersion ?? version;

        // Compute default NuGet output directory if not specified
        AbsolutePath? nugetOutputDir = null;
        if (cfg.NuGetOutputDir is not null)
        {
            nugetOutputDir = repoRoot / cfg.NuGetOutputDir;
        }
        else if (artifactsVersion is not null)
        {
            nugetOutputDir = repoRoot / "build" / "_artifacts" / artifactsVersion / "nuget";
        }

        // Map ProjectGroups to v1-style properties for backward compatibility
        var (hostsDir, pluginsDir, contractsDir, includeHosts, excludeHosts, includePlugins, excludePlugins, includeContracts, excludeContracts)
            = MapProjectGroupsToV1Properties(cfg, repoRoot);

        // Add explicit project paths
        var compileProjects = new List<string>(cfg.CompileProjects ?? Array.Empty<string>());
        var publishProjects = new List<string>(cfg.PublishProjects ?? Array.Empty<string>());
        var packProjects = new List<string>(cfg.PackProjects ?? Array.Empty<string>());

        // Extract projects from groups
        if (cfg.ProjectGroups is not null)
        {
            foreach (var (groupName, group) in cfg.ProjectGroups)
            {
                var projectPaths = DiscoverProjectsInGroup(repoRoot, group);

                switch (group.Action.ToLowerInvariant())
                {
                    case "publish":
                        publishProjects.AddRange(projectPaths);
                        break;
                    case "pack":
                        packProjects.AddRange(projectPaths);
                        break;
                    case "compile":
                        compileProjects.AddRange(projectPaths);
                        break;
                    default:
                        global::Serilog.Log.Warning($"Unknown action '{group.Action}' in group '{groupName}', treating as 'compile'");
                        compileProjects.AddRange(projectPaths);
                        break;
                }
            }
        }

        ArgumentNullException.ThrowIfNull(artifactsVersion);

        // Convert v2 ProjectGroups to v1-compatible BuildContext
        var context = new BuildContext
        {
            RepoRoot = repoRoot,
            HostsDir = hostsDir,
            PluginsDir = pluginsDir,
            ContractsDir = contractsDir,
            Solution = cfg.Solution is null ? null : repoRoot / cfg.Solution,
            NuGetOutputDir = nugetOutputDir,
            Version = version,
            ArtifactsVersion = artifactsVersion,
            IncludeHosts = includeHosts,
            ExcludeHosts = excludeHosts,
            IncludePlugins = includePlugins,
            ExcludePlugins = excludePlugins,
            IncludeContracts = includeContracts,
            ExcludeContracts = excludeContracts,
            CompileProjects = compileProjects.ToArray(),
            PublishProjects = publishProjects.ToArray(),
            PackProjects = packProjects.ToArray(),
            PackIncludeSymbols = cfg.PackIncludeSymbols,
            PackProperties = cfg.PackProperties ?? new Dictionary<string, string>(),
            SyncLocalNugetFeed = cfg.SyncLocalNugetFeed,
            LocalNugetFeedRoot = ResolveLocalFeedRoot(repoRoot, cfg.LocalNugetFeedRoot),
            NativeBuild = CreateNativeBuildContext(repoRoot, cfg.NativeBuild, artifactsVersion),
            RustBuild = CreateRustBuildContext(repoRoot, cfg.RustBuild, artifactsVersion),
            GoBuild = CreateGoBuildContext(repoRoot, cfg.GoBuild, artifactsVersion),
            UnityBuild = CreateUnityBuildContext(repoRoot, cfg.UnityBuild),
            GodotBuild = CreateGodotBuildContext(repoRoot, cfg.GodotBuild, artifactsVersion),
            MobileBuild = CreateMobileBuildContext(repoRoot, cfg.MobileBuild, artifactsVersion),
            UnityExport = CreateUnityExportContext(repoRoot, cfg.UnityExport, artifactsVersion)
        };

        return context;
    }

    private static GodotBuildContext? CreateGodotBuildContext(AbsolutePath repoRoot, GodotBuildConfig? cfg, string artifactsVersion)
    {
        if (cfg is null)
            return null;

        var platforms = cfg.Platforms?.Select(p => new GodotExportPlatformContext
        {
            Rid = p.Rid,
            PresetName = p.PresetName,
            BinaryName = p.BinaryName
        }).ToArray() ?? Array.Empty<GodotExportPlatformContext>();

        return new GodotBuildContext
        {
            ProjectRoot = repoRoot / cfg.ProjectRoot,
            ExecutablePathEnv = cfg.ExecutablePathEnv,
            ExecutablePath = cfg.ExecutablePath,
            AssemblyName = cfg.AssemblyName,
            Platforms = platforms,
            AndroidKeystorePath = cfg.AndroidKeystorePath,
            UseFastlaneForMobile = cfg.UseFastlaneForMobile,
            OutputDir = repoRoot / "build" / "_artifacts" / artifactsVersion / "godot"
        };
    }

    private static NativeBuildContext? CreateNativeBuildContext(AbsolutePath repoRoot, NativeBuildConfig? cfg, string artifactsVersion)
    {
        if (cfg is not null && !cfg.Enabled)
            return null;

        // Auto-detect if native directory exists
        var defaultSourceDir = repoRoot / "native";
        var cmakeListsPath = defaultSourceDir / "CMakeLists.txt";

        // If no config and no CMakeLists.txt, no native build
        if (cfg is null && !File.Exists(cmakeListsPath))
            return null;

        var sourceDir = cfg?.CMakeSourceDir is not null
            ? repoRoot / cfg.CMakeSourceDir
            : defaultSourceDir;

        var buildDir = cfg?.CMakeBuildDir is not null
            ? repoRoot / cfg.CMakeBuildDir
            : sourceDir / "build";

        var outputDir = cfg?.OutputDir is not null
            ? repoRoot / cfg.OutputDir
            : repoRoot / "build" / "_artifacts" / artifactsVersion / "native";

        return new NativeBuildContext
        {
            Enabled = cfg?.Enabled ?? true,
            CMakeSourceDir = sourceDir,
            CMakeBuildDir = buildDir,
            CMakePreset = cfg?.CMakePreset,
            CMakeOptions = cfg?.CMakeOptions ?? Array.Empty<string>(),
            BuildConfig = cfg?.BuildConfig ?? "Release",
            AutoDetectVcpkg = cfg?.AutoDetectVcpkg ?? true,
            OutputDir = outputDir,
            ArtifactPatterns = cfg?.ArtifactPatterns ?? new[] { "*.dll", "*.so", "*.dylib", "*.lib", "*.a" },
            CustomCommands = cfg?.CustomCommands ?? Array.Empty<string>(),
            Platform = cfg?.Platform
        };
    }

    private static RustBuildContext? CreateRustBuildContext(AbsolutePath repoRoot, RustBuildConfig? cfg, string artifactsVersion)
    {
        if (cfg is not null && !cfg.Enabled)
            return null;

        // If no config provided, no Rust build
        if (cfg is null)
            return null;

        var manifestDir = cfg.CargoManifestDir is not null
            ? repoRoot / cfg.CargoManifestDir
            : repoRoot;

        var outputDir = cfg.OutputDir is not null
            ? repoRoot / cfg.OutputDir
            : repoRoot / "build" / "_artifacts" / artifactsVersion / "rust";

        return new RustBuildContext
        {
            Enabled = cfg.Enabled,
            CargoManifestDir = manifestDir,
            Profile = cfg.Profile,
            Features = cfg.Features ?? Array.Empty<string>(),
            TargetTriple = cfg.TargetTriple,
            OutputDir = outputDir,
            ArtifactPatterns = cfg.ArtifactPatterns ?? new[] { "*.dll", "*.so", "*.dylib", "*.exe" }
        };
    }

    private static GoBuildContext? CreateGoBuildContext(AbsolutePath repoRoot, GoBuildConfig? cfg, string artifactsVersion)
    {
        if (cfg is not null && !cfg.Enabled)
            return null;

        // If no config provided, no Go build
        if (cfg is null)
            return null;

        var moduleDir = cfg.GoModuleDir is not null
            ? repoRoot / cfg.GoModuleDir
            : repoRoot;

        var outputDir = cfg.OutputDir is not null
            ? repoRoot / cfg.OutputDir
            : repoRoot / "build" / "_artifacts" / artifactsVersion / "go";

        return new GoBuildContext
        {
            Enabled = cfg.Enabled,
            GoModuleDir = moduleDir,
            BuildFlags = cfg.BuildFlags ?? Array.Empty<string>(),
            OutputBinary = cfg.OutputBinary,
            OutputDir = outputDir,
            EnvVars = cfg.EnvVars ?? new Dictionary<string, string>()
        };
    }

    private static UnityBuildContext? CreateUnityBuildContext(AbsolutePath repoRoot, UnityBuildJsonConfig? cfg)
    {
        if (cfg is null)
            return null;

        var unityRoot = Path.IsPathRooted(cfg.UnityProjectRoot)
            ? (AbsolutePath)cfg.UnityProjectRoot
            : repoRoot / cfg.UnityProjectRoot;

        var packages = (cfg.Packages ?? Array.Empty<UnityPackageMappingConfig>())
            .Select(p => CreateUnityPackageMapping(repoRoot, p))
            .ToArray();

        return new UnityBuildContext
        {
            TargetFramework = cfg.TargetFramework ?? "netstandard2.1",
            UnityProjectRoot = unityRoot,
            Packages = packages
        };
    }

    private static MobileBuildContext? CreateMobileBuildContext(AbsolutePath repoRoot, MobileBuildConfig? cfg, string artifactsVersion)
    {
        if (cfg is not null && !cfg.Enabled)
            return null;

        // If no config provided, check if mobile/ directory exists
        var defaultMobileRoot = repoRoot / "mobile";
        if (cfg is null && !Directory.Exists(defaultMobileRoot))
            return null;

        var mobileRoot = cfg?.MobileRoot is not null
            ? repoRoot / cfg.MobileRoot
            : defaultMobileRoot;

        var outputDir = cfg?.OutputDir is not null
            ? repoRoot / cfg.OutputDir
            : repoRoot / "build" / "_artifacts" / artifactsVersion / "mobile";

        MobilePlatformContext? ios = null;
        var iosDir = mobileRoot / "ios";
        if (Directory.Exists(iosDir) && (cfg?.Ios?.Enabled ?? true))
        {
            ios = CreateMobilePlatformContext(iosDir, cfg?.Ios);
        }

        MobilePlatformContext? android = null;
        var androidDir = mobileRoot / "android";
        if (Directory.Exists(androidDir) && (cfg?.Android?.Enabled ?? true))
        {
            android = CreateMobilePlatformContext(androidDir, cfg?.Android);
        }

        if (ios is null && android is null)
            return null;

        return new MobileBuildContext
        {
            Enabled = cfg?.Enabled ?? true,
            MobileRoot = mobileRoot,
            Ios = ios,
            Android = android,
            OutputDir = outputDir
        };
    }

    private static MobilePlatformContext CreateMobilePlatformContext(AbsolutePath workingDir, MobilePlatformConfig? cfg)
    {
        return new MobilePlatformContext
        {
            Enabled = cfg?.Enabled ?? true,
            WorkingDir = workingDir,
            EnvVars = cfg?.EnvVars ?? new Dictionary<string, string>(),
            BuildLane = cfg?.BuildLane ?? "build",
            BetaLane = cfg?.BetaLane ?? "beta",
            ReleaseLane = cfg?.ReleaseLane ?? "release"
        };
    }

    private static UnityExportContext? CreateUnityExportContext(AbsolutePath repoRoot, UnityExportConfig? cfg, string artifactsVersion)
    {
        if (cfg is null)
            return null;

        var outputDir = cfg.OutputDir is not null
            ? repoRoot / cfg.OutputDir
            : repoRoot / "build" / "_artifacts" / artifactsVersion / "unity-export";

        var platforms = (cfg.Platforms ?? Array.Empty<UnityExportPlatformConfig>())
            .Select(p => new UnityExportPlatformContext
            {
                BuildTarget = p.BuildTarget,
                OutputName = p.OutputName ?? p.BuildTarget,
                BuildArgs = p.BuildArgs ?? new Dictionary<string, string>()
            }).ToArray();

        return new UnityExportContext
        {
            ProjectRoot = repoRoot / cfg.ProjectRoot,
            EditorPath = cfg.EditorPath,
            EditorPathEnv = cfg.EditorPathEnv ?? "UNITY_EDITOR_PATH",
            ExecuteMethod = cfg.ExecuteMethod ?? "UnifyBuild.Editor.BuildScript.Build",
            Platforms = platforms,
            UseFastlaneForMobile = cfg.UseFastlaneForMobile,
            OutputDir = outputDir
        };
    }

    private static UnityPackageMapping CreateUnityPackageMapping(AbsolutePath repoRoot, UnityPackageMappingConfig cfg)
    {
        var sourceProjects = new List<string>(cfg.SourceProjects ?? Array.Empty<string>());

        // Expand glob patterns to discover .csproj files
        if (cfg.SourceProjectGlobs is not null)
        {
            foreach (var glob in cfg.SourceProjectGlobs)
            {
                var globDir = repoRoot / glob;
                if (Directory.Exists(globDir))
                {
                    // Glob is a directory - find all .csproj files in immediate subdirectories
                    var csprojFiles = Directory.GetDirectories(globDir)
                        .SelectMany(d => Directory.GetFiles(d, "*.csproj"))
                        .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                                    && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
                    sourceProjects.AddRange(csprojFiles);
                }
            }
        }

        return new UnityPackageMapping
        {
            PackageName = cfg.PackageName,
            ScopedIndex = cfg.ScopedIndex,
            SourceProjects = sourceProjects.ToArray(),
            SourceProjectGlobs = cfg.SourceProjectGlobs ?? Array.Empty<string>(),
            DependencyDlls = cfg.DependencyDlls ?? Array.Empty<string>()
        };
    }

    private static (AbsolutePath hostsDir, AbsolutePath pluginsDir, AbsolutePath? contractsDir,
                    string[] includeHosts, string[] excludeHosts,
                    string[] includePlugins, string[] excludePlugins,
                    string[] includeContracts, string[] excludeContracts)
        MapProjectGroupsToV1Properties(BuildJsonConfig cfg, AbsolutePath repoRoot)
    {
        // For backward compatibility, map well-known group names to v1 properties
        // This allows existing UnifyBuildBase targets to work with v2 configs

        if (cfg.ProjectGroups is null)
        {
            // No groups - use defaults
            return (
                repoRoot / "project" / "hosts",
                repoRoot / "project" / "plugins",
                null,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()
            );
        }

        // Try to map common group names to v1 properties
        var hostsGroup = cfg.ProjectGroups.FirstOrDefault(kvp =>
            kvp.Key.Equals("executables", StringComparison.OrdinalIgnoreCase) ||
            kvp.Key.Equals("hosts", StringComparison.OrdinalIgnoreCase) ||
            kvp.Key.Equals("apps", StringComparison.OrdinalIgnoreCase)).Value;

        var pluginsGroup = cfg.ProjectGroups.FirstOrDefault(kvp =>
            kvp.Key.Equals("plugins", StringComparison.OrdinalIgnoreCase) ||
            kvp.Key.Equals("libraries", StringComparison.OrdinalIgnoreCase) ||
            kvp.Key.Equals("libs", StringComparison.OrdinalIgnoreCase)).Value;

        var contractsGroup = cfg.ProjectGroups.FirstOrDefault(kvp =>
            kvp.Key.Equals("contracts", StringComparison.OrdinalIgnoreCase) ||
            kvp.Key.Equals("packages", StringComparison.OrdinalIgnoreCase) ||
            kvp.Key.Equals("abstractions", StringComparison.OrdinalIgnoreCase)).Value;

        return (
            hostsGroup is not null ? repoRoot / hostsGroup.SourceDir : repoRoot / "project" / "hosts",
            pluginsGroup is not null ? repoRoot / pluginsGroup.SourceDir : repoRoot / "project" / "plugins",
            contractsGroup is not null ? repoRoot / contractsGroup.SourceDir : null,
            hostsGroup?.Include ?? Array.Empty<string>(),
            hostsGroup?.Exclude ?? Array.Empty<string>(),
            pluginsGroup?.Include ?? Array.Empty<string>(),
            pluginsGroup?.Exclude ?? Array.Empty<string>(),
            contractsGroup?.Include ?? Array.Empty<string>(),
            contractsGroup?.Exclude ?? Array.Empty<string>()
        );
    }

    private static List<string> DiscoverProjectsInGroup(AbsolutePath repoRoot, ProjectGroup group)
    {
        var sourceDir = repoRoot / group.SourceDir;
        if (!Directory.Exists(sourceDir))
        {
            global::Serilog.Log.Warning($"Source directory '{sourceDir}' does not exist, skipping group");
            return new List<string>();
        }

        // Use EnumerationOptions for faster file enumeration, skipping known non-project directories
        var allProjects = EnumerateProjectFiles(sourceDir).ToList();

        // Apply include filter
        if (group.Include is not null && group.Include.Length > 0)
        {
            var includeSet = new HashSet<string>(group.Include, StringComparer.OrdinalIgnoreCase);
            allProjects = allProjects.Where(p => includeSet.Contains(Path.GetFileNameWithoutExtension(p))).ToList();
        }

        // Apply exclude filter
        if (group.Exclude is not null && group.Exclude.Length > 0)
        {
            var excludeSet = new HashSet<string>(group.Exclude, StringComparer.OrdinalIgnoreCase);
            allProjects = allProjects.Where(p => !excludeSet.Contains(Path.GetFileNameWithoutExtension(p))).ToList();
        }

        return allProjects;
    }

    /// <summary>
    /// Known directories to skip during project discovery for performance.
    /// </summary>
    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", ".vs", ".idea", "TestResults", "packages"
    };

    /// <summary>
    /// Recursively enumerates .csproj files, skipping known non-project directories early
    /// for better performance in large repositories.
    /// </summary>
    private static IEnumerable<string> EnumerateProjectFiles(string directory)
    {
        if (!Directory.Exists(directory))
            yield break;

        // Yield .csproj files in current directory
        foreach (var file in Directory.EnumerateFiles(directory, "*.csproj"))
        {
            yield return file;
        }

        // Recurse into subdirectories, skipping known non-project dirs
        foreach (var subDir in Directory.EnumerateDirectories(directory))
        {
            var dirName = Path.GetFileName(subDir);
            if (dirName.Length > 0 && dirName[0] == '.')
                continue; // Skip all hidden directories
            if (SkippedDirectories.Contains(dirName))
                continue;

            foreach (var file in EnumerateProjectFiles(subDir))
                yield return file;
        }
    }

    private static string? GetEnv(string? name)
        => string.IsNullOrWhiteSpace(name) ? null : Environment.GetEnvironmentVariable(name);

    /// <summary>
    /// Derives a SemVer from the consumer repository's own git state when no explicit
    /// version was supplied via config or environment. Mirrors the resolver used by the
    /// Godot app repos (tools/gitversion-semver.ps1): prefer GitVersion when it reports a
    /// real <c>VersionSourceSha</c>, otherwise fall back to <c>git describe</c> — bumping
    /// the patch because the commits are ahead of the tag. This keeps the version correct
    /// for any git repo even when the build is invoked directly (bypassing a Taskfile/CI
    /// that would otherwise export the version env), instead of silently stamping 0.1.0.
    /// </summary>
    /// <param name="repoRoot">
    /// The consumer repository root (NUKE RootDirectory). All git commands run against this
    /// path explicitly (<c>git -C</c>), so the version always reflects the consumer repo and
    /// never an enclosing repository or the build tool's own checkout.
    /// </param>
    /// <returns>
    /// The derived SemVer, or null when <paramref name="repoRoot"/> is not a git work tree,
    /// git is unavailable, or no version tag can be found — letting the caller apply its
    /// own default.
    /// </returns>
    internal static string? TryDeriveVersionFromGit(AbsolutePath repoRoot)
    {
        // Only derive inside an actual git work tree. Keeps non-git consumers (and unit-test
        // temp dirs) on their configured default path rather than reading a parent repo.
        if (RunProcess("git", $"-C \"{repoRoot}\" rev-parse --is-inside-work-tree", repoRoot)?.Trim() != "true")
            return null;

        // 1. Prefer GitVersion when it has resolved a real VersionSourceSha (clean repos / CI).
        var gvJson = RunProcess("dotnet", "tool run dotnet-gitversion /output json", repoRoot);
        if (!string.IsNullOrWhiteSpace(gvJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(gvJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("VersionSourceSha", out var sha)
                    && sha.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(sha.GetString())
                    && root.TryGetProperty("SemVer", out var sem)
                    && sem.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(sem.GetString()))
                {
                    return sem.GetString();
                }
            }
            catch (JsonException)
            {
                // Unparseable GitVersion output — fall through to git describe.
            }
        }

        // 2. Fall back to `git describe`. Handles the empty-VersionSourceSha trap that confuses
        //    GitVersion in agent-driven repos (e.g. internal refs/codex/* pointing at trees).
        var describe = RunProcess("git", $"-C \"{repoRoot}\" describe --tags --long --always", repoRoot)?.Trim();
        if (!string.IsNullOrWhiteSpace(describe))
        {
            var m = Regex.Match(describe, @"^v?(\d+)\.(\d+)\.(\d+)-(\d+)-g[0-9a-fA-F]+$");
            if (m.Success)
            {
                var major = m.Groups[1].Value;
                var minor = m.Groups[2].Value;
                var patch = int.Parse(m.Groups[3].Value) + 1; // commits are ahead of the tagged patch
                var commits = m.Groups[4].Value;
                return $"{major}.{minor}.{patch}-{commits}";
            }
        }

        return null;
    }

    /// <summary>
    /// Runs a child process and returns its trimmed stdout on a zero exit, or null on any
    /// failure (process missing, non-zero exit, or timeout). stdout and stderr are drained
    /// concurrently to avoid pipe-buffer deadlocks. Never throws — callers treat null as
    /// "could not determine" and fall back.
    /// </summary>
    private static string? RunProcess(string fileName, string arguments, AbsolutePath workingDirectory)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            if (!process.Start())
                return null;

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(15_000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                return null;
            }

            var stdout = stdoutTask.GetAwaiter().GetResult();
            stderrTask.GetAwaiter().GetResult();
            return process.ExitCode == 0 ? stdout : null;
        }
        catch
        {
            // git / dotnet not on PATH, or the process otherwise failed to launch.
            return null;
        }
    }

    /// <summary>
    /// Resolve a local NuGet feed root path from build.config.json. Absolute paths
    /// are used as-is; relative paths resolve against the repository root.
    /// Returns null if the input is null or whitespace.
    /// </summary>
    private static AbsolutePath? ResolveLocalFeedRoot(AbsolutePath repoRoot, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        return Path.IsPathRooted(path) ? (AbsolutePath)path : repoRoot / path;
    }
}
