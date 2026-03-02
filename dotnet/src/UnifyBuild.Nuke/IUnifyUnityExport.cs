using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Serilog;

namespace UnifyBuild.Nuke;

/// <summary>
/// Component for exporting Unity projects to platform builds.
///
/// Desktop platforms (Windows, macOS, Linux) produce final binaries directly.
/// Mobile platforms export native projects for Fastlane to build:
///   - Android: exports Gradle project (exportAsGoogleAndroidProject = true)
///   - iOS: exports Xcode project (default Unity behavior)
///
/// The unified mobile pipeline:
///   Unity exports native project → Fastlane builds + signs → Fastlane distributes
/// </summary>
public interface IUnifyUnityExport : IUnifyBuildConfig
{
    /// <summary>
    /// Export Unity project for all configured platforms.
    /// </summary>
    Target UnityExport => _ => _
        .Description("Export Unity project for all configured platforms")
        .Executes(() =>
        {
            var config = UnifyConfig.UnityExport;
            if (config is null)
            {
                Log.Information("No Unity export configuration found. Skipping.");
                return;
            }

            var editorPath = ResolveUnityEditorPath(config)
                ?? throw new InvalidOperationException(
                    $"Unity Editor not found. Set {config.EditorPathEnv} env var or configure editorPath in build.config.json.");

            Log.Information("Using Unity Editor: {EditorPath}", editorPath);

            foreach (var platform in config.Platforms)
            {
                ExportPlatform(config, editorPath, platform);
            }
        });

    /// <summary>
    /// Export Unity project for desktop platforms only (Windows, macOS, Linux).
    /// </summary>
    Target UnityExportDesktop => _ => _
        .Description("Export Unity project for desktop platforms only")
        .Executes(() =>
        {
            var config = UnifyConfig.UnityExport;
            if (config is null) return;

            var editorPath = ResolveUnityEditorPath(config)
                ?? throw new InvalidOperationException("Unity Editor not found.");

            foreach (var platform in config.Platforms.Where(p => !p.IsMobile))
                ExportPlatform(config, editorPath, platform);
        });

    /// <summary>
    /// Export Unity project as native projects for mobile platforms (Gradle/Xcode).
    /// The exported projects are then built by Fastlane via IUnifyMobile.
    /// </summary>
    Target UnityExportMobile => _ => _
        .Description("Export Unity mobile platforms as Gradle/Xcode projects for Fastlane")
        .Executes(() =>
        {
            var config = UnifyConfig.UnityExport;
            if (config is null) return;

            var editorPath = ResolveUnityEditorPath(config)
                ?? throw new InvalidOperationException("Unity Editor not found.");

            foreach (var platform in config.Platforms.Where(p => p.IsMobile))
                ExportPlatform(config, editorPath, platform);
        });

    // --- helpers ---

    sealed void ExportPlatform(UnityExportContext config, string editorPath, UnityExportPlatformContext platform)
    {
        var outputDir = config.OutputDir! / platform.BuildTarget;
        outputDir.CreateOrCleanDirectory();

        var outputPath = outputDir / platform.OutputName;

        Log.Information("Exporting Unity [{BuildTarget}] -> {OutputPath}", platform.BuildTarget, outputPath);

        // Pass build configuration via environment variables so the BuildScript can read them.
        // Start with the current process environment so Unity inherits PATH, APPDATA, etc.
        // (NUKE's ProcessTasks.StartProcess replaces the entire env when environmentVariables is set)
        var envVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            envVars[entry.Key!.ToString()!] = entry.Value?.ToString() ?? "";

        // Layer on platform-specific build args
        foreach (var kv in platform.BuildArgs)
            envVars[kv.Key] = kv.Value;

        envVars["UNIFYBUILD_TARGET"] = platform.BuildTarget;
        envVars["UNIFYBUILD_OUTPUT"] = outputPath;
        envVars["UNIFYBUILD_VERSION"] = UnifyConfig.Version ?? "0.1.0";

        // For mobile: signal the BuildScript to export native project instead of final binary
        if (platform.IsMobile)
        {
            envVars["UNIFYBUILD_EXPORT_PROJECT"] = "true";

            if (platform.BuildTarget.Equals("Android", StringComparison.OrdinalIgnoreCase))
                envVars["UNIFYBUILD_ANDROID_EXPORT_GRADLE"] = "true";
        }

        var args = $"-batchmode -nographics -quit " +
                   $"-projectPath \"{config.ProjectRoot}\" " +
                   $"-executeMethod {config.ExecuteMethod} " +
                   $"-buildTarget {platform.BuildTarget} " +
                   $"-logFile \"{outputDir / "unity-build.log"}\"";

        var process = ProcessTasks.StartProcess(
            editorPath,
            args,
            workingDirectory: config.ProjectRoot,
            environmentVariables: envVars.AsReadOnly());

        process.AssertZeroExitCode();

        // For mobile: set EXPORTED_PROJECT_DIR so Fastlane knows where to find the project
        if (platform.IsMobile)
        {
            // For Android: inject Gradle wrapper so Fastlane/Gradle can build the exported project
            if (platform.BuildTarget.Equals("Android", StringComparison.OrdinalIgnoreCase))
                InjectGradleWrapper(editorPath, outputPath);

            Environment.SetEnvironmentVariable("EXPORTED_PROJECT_DIR", outputPath);
            Log.Information("Exported {BuildTarget} native project to {OutputPath}. " +
                "Use MobileBuildAndroidFromProject or MobileBuildIosFromProject to build with Fastlane.",
                platform.BuildTarget, outputPath);
        }
        else
        {
            Log.Information("Unity export complete: {BuildTarget} -> {OutputDir}", platform.BuildTarget, outputDir);
        }
    }

    /// <summary>
    /// Injects a Gradle wrapper into the exported Android project so it can be built
    /// by Fastlane or standalone Gradle without requiring a system Gradle installation.
    /// Uses the Gradle version bundled with the Unity Editor.
    /// </summary>
    sealed void InjectGradleWrapper(string editorPath, AbsolutePath projectDir)
    {
        // Resolve Unity's bundled Gradle and JDK paths from the editor location
        var editorDir = Path.GetDirectoryName(editorPath)!;
        var androidPlayerDir = Path.Combine(editorDir, "Data", "PlaybackEngines", "AndroidPlayer");
        var gradleLibDir = Path.Combine(androidPlayerDir, "Tools", "gradle", "lib");
        var jdkDir = Path.Combine(androidPlayerDir, "OpenJDK");

        // Find the Gradle version from the bundled jar
        string? gradleVersion = null;
        if (Directory.Exists(gradleLibDir))
        {
            var launcherJar = Directory.GetFiles(gradleLibDir, "gradle-launcher-*.jar").FirstOrDefault();
            if (launcherJar is not null)
            {
                var fileName = Path.GetFileNameWithoutExtension(launcherJar);
                gradleVersion = fileName.Replace("gradle-launcher-", "");
            }
        }

        if (gradleVersion is null)
        {
            Log.Warning("Could not detect bundled Gradle version. Skipping wrapper injection.");
            return;
        }

        Log.Information("Injecting Gradle {Version} wrapper into exported project", gradleVersion);

        // Create gradle/wrapper directory
        var wrapperDir = projectDir / "gradle" / "wrapper";
        wrapperDir.CreateDirectory();

        // Write gradle-wrapper.properties
        var propsContent = $"""
            distributionBase=GRADLE_USER_HOME
            distributionPath=wrapper/dists
            distributionUrl=https\://services.gradle.org/distributions/gradle-{gradleVersion}-bin.zip
            networkTimeout=10000
            validateDistributionUrl=true
            zipStoreBase=GRADLE_USER_HOME
            zipStorePath=wrapper/dists
            """.Replace("            ", "");
        File.WriteAllText(wrapperDir / "gradle-wrapper.properties", propsContent);

        // Copy gradle-wrapper.jar and gradlew scripts from Unity's bundled templates.
        // Unity ships these in Tools/VisualStudioGradleTemplates/ alongside the Gradle distribution.
        // This avoids running 'gradle wrapper' via ProcessTasks which has quoting issues with
        // paths containing spaces (e.g., "C:\Program Files\Unity\...").
        var templateDir = Path.Combine(androidPlayerDir, "Tools", "VisualStudioGradleTemplates");
        var sourceWrapperJar = Path.Combine(templateDir, "gradle-wrapper.jar");

        if (File.Exists(sourceWrapperJar))
        {
            File.Copy(sourceWrapperJar, wrapperDir / "gradle-wrapper.jar", overwrite: true);
            Log.Information("Copied gradle-wrapper.jar from Unity templates");
        }
        else
        {
            Log.Warning("gradle-wrapper.jar not found at {Path}. Gradle wrapper may not work.", sourceWrapperJar);
        }

        // Copy gradlew.bat (Windows)
        var sourceGradlewBat = Path.Combine(templateDir, "gradlew.bat");
        if (File.Exists(sourceGradlewBat))
        {
            File.Copy(sourceGradlewBat, projectDir / "gradlew.bat", overwrite: true);
            Log.Information("Copied gradlew.bat from Unity templates");
        }

        // Generate gradlew (Unix) — Unity only bundles gradlew.bat, so we write the standard script.
        // This is the canonical Gradle wrapper shell script used by all Gradle projects.
        WriteGradlewUnixScript(projectDir / "gradlew");

        // Set JAVA_HOME and ANDROID_HOME for downstream Gradle/Fastlane builds
        if (Directory.Exists(jdkDir))
            Environment.SetEnvironmentVariable("JAVA_HOME", jdkDir);

        var sdkDir = Path.Combine(androidPlayerDir, "SDK");
        if (Directory.Exists(sdkDir))
            Environment.SetEnvironmentVariable("ANDROID_HOME", sdkDir);

        Log.Information("Gradle wrapper injected successfully");
    }


    sealed string? ResolveUnityEditorPath(UnityExportContext config)
    {
        // 1. Explicit path from config
        if (!string.IsNullOrEmpty(config.EditorPath) && File.Exists(config.EditorPath))
            return config.EditorPath;

        // 2. Environment variable
        var envPath = Environment.GetEnvironmentVariable(config.EditorPathEnv);
        if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
            return envPath;

        // 3. Common install locations
        return TryDetectUnityEditor();
    }

    sealed string? TryDetectUnityEditor()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var hubEditorsDir = Path.Combine(programFiles, "Unity", "Hub", "Editor");
            if (Directory.Exists(hubEditorsDir))
            {
                var latest = Directory.GetDirectories(hubEditorsDir)
                    .OrderByDescending(d => Path.GetFileName(d))
                    .FirstOrDefault();
                if (latest is not null)
                {
                    var exe = Path.Combine(latest, "Editor", "Unity.exe");
                    if (File.Exists(exe)) return exe;
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var hubEditorsDir = "/Applications/Unity/Hub/Editor";
            if (Directory.Exists(hubEditorsDir))
            {
                var latest = Directory.GetDirectories(hubEditorsDir)
                    .OrderByDescending(d => Path.GetFileName(d))
                    .FirstOrDefault();
                if (latest is not null)
                {
                    var app = Path.Combine(latest, "Unity.app", "Contents", "MacOS", "Unity");
                    if (File.Exists(app)) return app;
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var hubEditorsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Unity", "Hub", "Editor");
            if (Directory.Exists(hubEditorsDir))
            {
                var latest = Directory.GetDirectories(hubEditorsDir)
                    .OrderByDescending(d => Path.GetFileName(d))
                    .FirstOrDefault();
                if (latest is not null)
                {
                    var exe = Path.Combine(latest, "Editor", "Unity");
                    if (File.Exists(exe)) return exe;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Writes the standard Gradle wrapper Unix shell script.
    /// This is the canonical gradlew script that ships with every Gradle project.
    /// </summary>
    sealed void WriteGradlewUnixScript(string path)
    {
        var script = """
            #!/bin/sh

            ##############################################################################
            ##
            ##  Gradle start up script for POSIX generated by UnifyBuild
            ##
            ##############################################################################

            # Attempt to set APP_HOME
            # Resolve links: $0 may be a link
            app_path=$0
            while
                APP_HOME=${app_path%"${app_path##*/}"}
                [ -h "$app_path" ]
            do
                ls=$( ls -ld -- "$app_path" )
                link=${ls#*' -> '}
                case $link in
                    /*)   app_path=$link ;;
                    *)    app_path=$APP_HOME$link ;;
                esac
            done
            APP_HOME=$( cd "${APP_HOME:-./}" > /dev/null && pwd -P ) || exit

            # Use the maximum available, or set MAX_FD != -1 to use that value.
            MAX_FD=maximum

            warn () { echo "$*"; } >&2
            die () { echo; echo "$*"; echo; exit 1; } >&2

            # OS specific support
            cygwin=false
            msys=false
            darwin=false
            nonstop=false
            case "$( uname )" in
                CYGWIN* )         cygwin=true  ;;
                Darwin* )         darwin=true   ;;
                MSYS* | MINGW* )  msys=true     ;;
                NonStop* )        nonstop=true  ;;
            esac

            CLASSPATH=$APP_HOME/gradle/wrapper/gradle-wrapper.jar

            # Determine the Java command to use to start the JVM.
            if [ -n "$JAVA_HOME" ] ; then
                if [ -x "$JAVA_HOME/jre/sh/java" ] ; then
                    JAVACMD=$JAVA_HOME/jre/sh/java
                else
                    JAVACMD=$JAVA_HOME/bin/java
                fi
                if [ ! -x "$JAVACMD" ] ; then
                    die "ERROR: JAVA_HOME is set to an invalid directory: $JAVA_HOME"
                fi
            else
                JAVACMD=java
                if ! command -v java >/dev/null 2>&1 ; then
                    die "ERROR: JAVA_HOME is not set and no 'java' command could be found in your PATH."
                fi
            fi

            # Increase the maximum file descriptors if we can.
            if ! "$cygwin" && ! "$darwin" && ! "$nonstop" ; then
                case $MAX_FD in
                    max*)
                        MAX_FD=$( ulimit -H -n ) ||
                            warn "Could not query maximum file descriptor limit"
                    ;;
                esac
                case $MAX_FD in
                    '' | soft) ;;
                    *)
                        ulimit -n "$MAX_FD" ||
                            warn "Could not set maximum file descriptor limit to $MAX_FD"
                    ;;
                esac
            fi

            # Collect all arguments for the java command, stracks://gradle.org/m2
            # For Cygwin or MSYS, switch paths to Windows format before running java
            if "$cygwin" || "$msys" ; then
                APP_HOME=$( cygpath --path --mixed "$APP_HOME" )
                CLASSPATH=$( cygpath --path --mixed "$CLASSPATH" )
                JAVACMD=$( cygpath --unix "$JAVACMD" )
            fi

            exec "$JAVACMD" $DEFAULT_JVM_OPTS $JAVA_OPTS $GRADLE_OPTS \
                "-Dorg.gradle.appname=$APP_BASE_NAME" \
                -classpath "$CLASSPATH" \
                org.gradle.wrapper.GradleWrapperMain "$@"
            """.Replace("            ", "");
        File.WriteAllText(path, script.Replace("\r\n", "\n"));
        Log.Information("Generated gradlew Unix script");
    }
}
