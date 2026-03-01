using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using System.Text.Json;

namespace UnifyBuild.Nuke;

public interface IUnifyGodot : IUnifyCompile
{
    Target BuildGodot => _ => _
        .DependsOn<IUnifyCompile>(x => x.Compile)
        .Executes(() =>
        {
            var config = UnifyConfig.GodotBuild;
            if (config is null)
                return;

            var godotPath = config.ExecutablePath;
            if (string.IsNullOrEmpty(godotPath) && !string.IsNullOrEmpty(config.ExecutablePathEnv))
            {
                godotPath = Environment.GetEnvironmentVariable(config.ExecutablePathEnv);
            }

            if (string.IsNullOrEmpty(godotPath) || !File.Exists(godotPath))
            {
                throw new InvalidOperationException($"Godot executable not found at '{godotPath}'. Verify GODOT env var.");
            }

            var projectRoot = config.ProjectRoot;
            var csproj = Directory.GetFiles(projectRoot, "*.csproj").FirstOrDefault();
            if (csproj == null)
            {
                throw new InvalidOperationException($"No .csproj found in Godot project root: {projectRoot}");
            }

            var assemblyName = config.AssemblyName ?? Path.GetFileNameWithoutExtension(csproj);
            var semver = UnifyConfig.Version ?? "0.1.0";
            var artifactsDir = UnifyConfig.RepoRoot / "build" / "_artifacts" / (UnifyConfig.ArtifactsVersion ?? semver);

            foreach (var platform in config.Platforms)
            {
                var rid = platform.Rid;
                var outDir = artifactsDir / rid;

                outDir.CreateOrCleanDirectory();

                var publishOut = projectRoot / ".godot" / "mono" / "temp" / "bin" / Configuration / $"publish-{rid}";

                global::Serilog.Log.Information($"Publishing .NET for {rid} (self-contained)...");
                DotNetPublish(s => s
                    .SetProject(csproj)
                    .SetConfiguration(Configuration)
                    .SetRuntime(rid)
                    .EnableSelfContained()
                    .SetOutput(publishOut)
                    .SetProcessArgumentConfigurator(a => a.Add("-v quiet"))
                );

                var exportPath = outDir / platform.BinaryName;

                global::Serilog.Log.Information($"Exporting [{rid}] -> {platform.PresetName}...");

                try
                {
                    var process = ProcessTasks.StartProcess(
                        godotPath,
                        $"--headless --path {projectRoot} --export-release {platform.PresetName} {exportPath}",
                        workingDirectory: projectRoot
                    );
                    process.AssertZeroExitCode();
                }
                catch (Exception ex)
                {
                    var binaryExists = rid == "osx-universal" || rid == "osx-x64" ? File.Exists(exportPath) : File.Exists(exportPath);
                    if (!binaryExists)
                    {
                        throw new InvalidOperationException($"Export failed for {rid} — no binary at {exportPath}", ex);
                    }
                }

                if (Directory.Exists(publishOut))
                {
                    if (rid == "osx-universal" || rid == "osx-x64")
                    {
                        var tempExtract = (AbsolutePath)Path.GetTempPath() / $"godot-mac-inject-{Guid.NewGuid():N}";
                        ZipFile.ExtractToDirectory(exportPath, tempExtract);
                        var appBundle = Directory.GetDirectories(tempExtract, "*.app").FirstOrDefault();
                        if (appBundle != null)
                        {
                            var dataDirName = platform.DataDirName.Replace("{AssemblyName}", assemblyName);
                            var resourcesDir = (AbsolutePath)appBundle / "Contents" / "Resources" / dataDirName;
                            resourcesDir.CreateDirectory();
                            CopyDirectoryRecursively(publishOut, resourcesDir, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);
                            
                            File.Delete(exportPath);
                            ZipFile.CreateFromDirectory(tempExtract, exportPath, CompressionLevel.Optimal, includeBaseDirectory: false);
                            
                            var dllCount = Directory.GetFiles(resourcesDir, "*.dll").Length;
                            global::Serilog.Log.Information($"Injected {dllCount} DLLs -> {Path.GetFileName(appBundle)}/Contents/Resources/{dataDirName}/");
                        }
                        tempExtract.DeleteDirectory();
                    }
                    else
                    {
                        var dataDirName = platform.DataDirName.Replace("{AssemblyName}", assemblyName);
                        var dataDir = outDir / dataDirName;
                        dataDir.CreateDirectory();
                        CopyDirectoryRecursively(publishOut, dataDir, DirectoryExistsPolicy.Merge, FileExistsPolicy.Overwrite);
                        var dllCount = Directory.GetFiles(dataDir, "*.dll").Length;
                        global::Serilog.Log.Information($"Copied {dllCount} DLLs -> {dataDirName}/");
                    }
                }

                var versionData = new
                {
                    SemVer = semver,
                    Platform = rid,
                    Preset = platform.PresetName,
                    ExportedAt = DateTime.UtcNow.ToString("o")
                };
                File.WriteAllText(outDir / "version.json", JsonSerializer.Serialize(versionData, new JsonSerializerOptions { WriteIndented = true }));
                
                global::Serilog.Log.Information($"Done: {outDir}");
            }
        });
}
