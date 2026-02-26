using System.Diagnostics;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;

namespace UnifyBuild.Nuke;

/// <summary>
/// Component for building Go projects.
/// Provides explicit 'GoBuild' target for building Go components.
/// </summary>
public interface IUnifyGo : IUnifyBuildConfig
{
    /// <summary>
    /// Build Go components using go build.
    /// </summary>
    Target GoBuild => _ => _
        .Description("Build Go projects")
        .Executes(() =>
        {
            var goConfig = UnifyConfig.GoBuild;
            if (goConfig is null)
            {
                Serilog.Log.Information("No Go build configuration found. Skipping.");
                return;
            }

            if (!goConfig.Enabled)
            {
                Serilog.Log.Information("Go build is disabled. Skipping.");
                return;
            }

            var goModPath = goConfig.GoModuleDir / "go.mod";
            if (!File.Exists(goModPath))
            {
                Serilog.Log.Warning("go.mod not found at {Path}. Skipping Go build.", goModPath);
                return;
            }

            if (!DetectGo())
            {
                Serilog.Log.Error("go not found in PATH. Please install Go: https://go.dev/dl/");
                return;
            }

            ExecuteGoBuild(goConfig);
            CollectGoArtifacts(goConfig);
        });

    private static bool DetectGo()
    {
        try
        {
            var process = ProcessTasks.StartProcess("go", "version", logOutput: false);
            process.AssertZeroExitCode();
            Serilog.Log.Information("Detected go: {Output}", process.Output.FirstOrDefault().Text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ExecuteGoBuild(GoBuildContext config)
    {
        Serilog.Log.Information("Building Go project in {Dir}", config.GoModuleDir);

        var args = "build";

        // Output binary via -o flag
        if (!string.IsNullOrEmpty(config.OutputBinary))
        {
            AbsolutePath outputPath;
            if (config.OutputDir is not null)
            {
                config.OutputDir.CreateDirectory();
                outputPath = config.OutputDir / config.OutputBinary;
            }
            else
            {
                outputPath = config.GoModuleDir! / config.OutputBinary;
            }

            args += $" -o \"{outputPath}\"";
        }

        // Build flags
        foreach (var flag in config.BuildFlags)
        {
            args += $" {flag}";
        }

        // Set up environment variables (GOOS, GOARCH, etc.)
        IReadOnlyDictionary<string, string>? envVars = null;
        if (config.EnvVars.Count > 0)
        {
            foreach (var (key, value) in config.EnvVars)
            {
                Serilog.Log.Information("Setting env: {Key}={Value}", key, value);
            }

            // Merge with current environment
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in Environment.GetEnvironmentVariables())
            {
                if (entry is System.Collections.DictionaryEntry de)
                {
                    merged[de.Key.ToString()!] = de.Value?.ToString() ?? "";
                }
            }
            foreach (var (key, value) in config.EnvVars)
            {
                merged[key] = value;
            }
            envVars = merged;
        }

        ProcessTasks.StartProcess("go", args, config.GoModuleDir, envVars)
            .AssertZeroExitCode();
    }

    private static void CollectGoArtifacts(GoBuildContext config)
    {
        if (config.OutputDir is null)
        {
            Serilog.Log.Debug("No output directory configured for Go artifacts");
            return;
        }

        config.OutputDir.CreateDirectory();

        // If OutputBinary was specified with -o pointing to OutputDir, the binary is already there.
        // If not, try to collect from the module directory.
        if (string.IsNullOrEmpty(config.OutputBinary))
        {
            // Go build without -o places binary in current directory
            // Try to find executables in the module directory
            var moduleDir = config.GoModuleDir!;
            var files = Directory.GetFiles(moduleDir, "*", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                // Check if file is executable (no extension on Linux/macOS, .exe on Windows)
                var ext = Path.GetExtension(file);
                if (string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(ext))
                {
                    try
                    {
                        // Only copy if it looks like a binary (not go.mod, go.sum, etc.)
                        if (file.EndsWith(".mod") || file.EndsWith(".sum") || file.EndsWith(".go"))
                            continue;

                        var destFile = config.OutputDir / Path.GetFileName(file);
                        File.Copy(file, destFile, overwrite: true);
                        Serilog.Log.Information("Collected Go artifact: {File}", Path.GetFileName(file));
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Debug("Skipped file {File}: {Error}", file, ex.Message);
                    }
                }
            }
        }
        else
        {
            // Binary was placed via -o, verify it exists
            var binaryPath = config.OutputDir / config.OutputBinary;
            if (File.Exists(binaryPath))
            {
                Serilog.Log.Information("Go artifact ready: {File}", config.OutputBinary);
            }
            else
            {
                Serilog.Log.Warning("Expected Go binary not found at {Path}", binaryPath);
            }
        }
    }
}
