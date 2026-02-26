using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;

namespace UnifyBuild.Nuke;

/// <summary>
/// Component for building Rust projects via Cargo.
/// Provides explicit 'RustBuild' target for building Rust components.
/// </summary>
public interface IUnifyRust : IUnifyBuildConfig
{
    /// <summary>
    /// Build Rust components using Cargo.
    /// </summary>
    Target RustBuild => _ => _
        .Description("Build Rust projects via Cargo")
        .Executes(() =>
        {
            var rustConfig = UnifyConfig.RustBuild;
            if (rustConfig is null)
            {
                Serilog.Log.Information("No Rust build configuration found. Skipping.");
                return;
            }

            if (!rustConfig.Enabled)
            {
                Serilog.Log.Information("Rust build is disabled. Skipping.");
                return;
            }

            var cargoTomlPath = rustConfig.CargoManifestDir / "Cargo.toml";
            if (!File.Exists(cargoTomlPath))
            {
                Serilog.Log.Warning("Cargo.toml not found at {Path}. Skipping Rust build.", cargoTomlPath);
                return;
            }

            if (!DetectCargo())
            {
                Serilog.Log.Error("cargo not found in PATH. Please install Rust: https://rustup.rs/");
                return;
            }

            ExecuteCargoBuild(rustConfig);
            CollectRustArtifacts(rustConfig);
        });

    private static bool DetectCargo()
    {
        try
        {
            var process = ProcessTasks.StartProcess("cargo", "--version", logOutput: false);
            process.AssertZeroExitCode();
            Serilog.Log.Information("Detected cargo: {Output}", process.Output.FirstOrDefault().Text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ExecuteCargoBuild(RustBuildContext config)
    {
        Serilog.Log.Information("Building Rust project with profile: {Profile}", config.Profile);

        var args = "build";

        // Profile: "dev" is the default debug profile, "release" uses --release flag,
        // custom profiles use --profile flag
        if (string.Equals(config.Profile, "release", StringComparison.OrdinalIgnoreCase))
        {
            args += " --release";
        }
        else if (!string.Equals(config.Profile, "dev", StringComparison.OrdinalIgnoreCase)
              && !string.Equals(config.Profile, "debug", StringComparison.OrdinalIgnoreCase))
        {
            args += $" --profile {config.Profile}";
        }

        // Features
        if (config.Features.Length > 0)
        {
            args += $" --features {string.Join(",", config.Features)}";
        }

        // Target triple
        if (!string.IsNullOrEmpty(config.TargetTriple))
        {
            args += $" --target {config.TargetTriple}";
        }

        ProcessTasks.StartProcess("cargo", args, config.CargoManifestDir)
            .AssertZeroExitCode();
    }

    private static void CollectRustArtifacts(RustBuildContext config)
    {
        if (config.OutputDir is null)
        {
            Serilog.Log.Debug("No output directory configured for Rust artifacts");
            return;
        }

        config.OutputDir.CreateDirectory();

        // Cargo output is in target/{profile}/ or target/{triple}/{profile}/
        var profileDir = string.Equals(config.Profile, "dev", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(config.Profile, "debug", StringComparison.OrdinalIgnoreCase)
            ? "debug"
            : config.Profile;

        AbsolutePath buildOutputDir;
        if (!string.IsNullOrEmpty(config.TargetTriple))
        {
            buildOutputDir = config.CargoManifestDir! / "target" / config.TargetTriple / profileDir;
        }
        else
        {
            buildOutputDir = config.CargoManifestDir! / "target" / profileDir;
        }

        if (!Directory.Exists(buildOutputDir))
        {
            Serilog.Log.Warning("Cargo build output directory not found: {Path}", buildOutputDir);
            return;
        }

        foreach (var pattern in config.ArtifactPatterns)
        {
            var files = Directory.GetFiles(buildOutputDir, pattern, SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                var destFile = config.OutputDir / Path.GetFileName(file);
                File.Copy(file, destFile, overwrite: true);
                Serilog.Log.Information("Collected Rust artifact: {File}", Path.GetFileName(file));
            }
        }
    }
}
