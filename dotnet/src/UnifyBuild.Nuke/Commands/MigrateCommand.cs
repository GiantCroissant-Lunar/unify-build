using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nuke.Common.IO;

namespace UnifyBuild.Nuke.Commands;

/// <summary>
/// Result of a migration operation.
/// </summary>
public sealed record MigrateResult(
    string OriginalPath,
    string BackupPath,
    string MigratedPath,
    List<string> Changes,
    bool IsValid
);

/// <summary>
/// Migrates old-format (v1) build.config.json files to the current v2 schema.
/// The v1 schema used domain-specific properties like hostsDir, pluginsDir, contractsDir
/// with include/exclude arrays. The v2 schema uses generic projectGroups.
/// </summary>
public sealed class MigrateCommand
{
    private static readonly string[] V1Properties =
    {
        "hostsDir", "pluginsDir", "contractsDir",
        "includeHosts", "excludeHosts",
        "includePlugins", "excludePlugins",
        "includeContracts", "excludeContracts"
    };

    /// <summary>
    /// Executes the migration: detects config version, creates backup, applies transformations, writes result.
    /// </summary>
    public MigrateResult Execute(AbsolutePath configPath)
    {
        var path = (string)configPath;
        if (!File.Exists(path))
            throw new FileNotFoundException($"Config file not found: {path}", path);

        var originalJson = File.ReadAllText(path);
        var changes = new List<string>();

        // Parse the original JSON
        using var doc = JsonDocument.Parse(originalJson, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        // Detect if this is a v1 config
        var fromVersion = DetectVersion(doc);
        if (fromVersion >= 2)
        {
            return new MigrateResult(
                OriginalPath: path,
                BackupPath: string.Empty,
                MigratedPath: path,
                Changes: new List<string> { "Config is already at v2 schema, no migration needed." },
                IsValid: true
            );
        }

        // Create backup
        var backupPath = CreateBackup(path);
        changes.Add($"Created backup at {backupPath}");

        // Apply migrations
        var migratedConfig = ApplyMigrations(doc, fromVersion);
        changes.AddRange(GetMigrationChanges(doc));

        // Serialize and write the migrated config
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var migratedJson = JsonSerializer.Serialize(migratedConfig, options);
        File.WriteAllText(path, migratedJson);

        // Validate the result by attempting to parse it
        bool isValid;
        try
        {
            var validateOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<BuildJsonConfig>(migratedJson, validateOptions);
            isValid = parsed?.ProjectGroups != null;
        }
        catch
        {
            isValid = false;
        }

        return new MigrateResult(
            OriginalPath: path,
            BackupPath: backupPath,
            MigratedPath: path,
            Changes: changes,
            IsValid: isValid
        );
    }

    /// <summary>
    /// Creates a backup of the original config file as {path}.bak.
    /// </summary>
    internal string CreateBackup(string configPath)
    {
        var backupPath = configPath + ".bak";
        File.Copy(configPath, backupPath, overwrite: true);
        return backupPath;
    }

    /// <summary>
    /// Applies migration rules to transform v1 config to v2 schema.
    /// </summary>
    internal BuildJsonConfig ApplyMigrations(JsonDocument original, int fromVersion)
    {
        if (fromVersion >= 2)
        {
            // Already v2, just deserialize as-is
            var json = original.RootElement.GetRawText();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<BuildJsonConfig>(json, options)
                   ?? new BuildJsonConfig();
        }

        // v1 → v2 migration
        return MigrateV1ToV2(original);
    }

    /// <summary>
    /// Detects the config version based on property names present.
    /// v1 configs have hostsDir/pluginsDir/contractsDir properties.
    /// v2 configs have projectGroups property.
    /// </summary>
    private static int DetectVersion(JsonDocument doc)
    {
        var root = doc.RootElement;

        // If it has projectGroups, it's v2
        if (root.TryGetProperty("projectGroups", out _) ||
            root.TryGetProperty("ProjectGroups", out _))
            return 2;

        // Check for v1 properties
        foreach (var prop in V1Properties)
        {
            if (root.TryGetProperty(prop, out _))
                return 1;
        }

        // Also check PascalCase variants
        if (root.TryGetProperty("HostsDir", out _) ||
            root.TryGetProperty("PluginsDir", out _) ||
            root.TryGetProperty("ContractsDir", out _))
            return 1;

        // Default to v2 if no v1 properties found
        return 2;
    }

    /// <summary>
    /// Transforms a v1 config into a v2 BuildJsonConfig.
    /// </summary>
    private static BuildJsonConfig MigrateV1ToV2(JsonDocument doc)
    {
        var root = doc.RootElement;
        var config = new BuildJsonConfig();

        // Preserve common properties
        config.Version = GetStringProperty(root, "version");
        config.VersionEnv = GetStringProperty(root, "versionEnv") ?? "Version";
        config.ArtifactsVersion = GetStringProperty(root, "artifactsVersion");
        config.Solution = GetStringProperty(root, "solution");
        config.NuGetOutputDir = GetStringProperty(root, "nuGetOutputDir") ?? GetStringProperty(root, "nugetOutputDir");
        config.PublishOutputDir = GetStringProperty(root, "publishOutputDir");

        if (TryGetBoolProperty(root, "packIncludeSymbols", out var packSymbols))
            config.PackIncludeSymbols = packSymbols;

        if (TryGetBoolProperty(root, "syncLocalNugetFeed", out var syncFeed))
            config.SyncLocalNugetFeed = syncFeed;

        config.LocalNugetFeedRoot = GetStringProperty(root, "localNugetFeedRoot");
        config.LocalNugetFeedFlatSubdir = GetStringProperty(root, "localNugetFeedFlatSubdir") ?? "flat";
        config.LocalNugetFeedHierarchicalSubdir = GetStringProperty(root, "localNugetFeedHierarchicalSubdir") ?? "hierarchical";
        config.LocalNugetFeedBaseUrl = GetStringProperty(root, "localNugetFeedBaseUrl");

        // Preserve explicit project arrays
        config.CompileProjects = GetStringArrayProperty(root, "compileProjects");
        config.PublishProjects = GetStringArrayProperty(root, "publishProjects");
        config.PackProjects = GetStringArrayProperty(root, "packProjects");

        // Transform v1 directory-based properties into v2 projectGroups
        config.ProjectGroups = new Dictionary<string, ProjectGroup>();

        // hostsDir + includeHosts/excludeHosts → "executables" group with action "publish"
        var hostsDir = GetStringProperty(root, "hostsDir");
        if (hostsDir != null)
        {
            config.ProjectGroups["executables"] = new ProjectGroup
            {
                SourceDir = hostsDir,
                Action = "publish",
                Include = GetStringArrayProperty(root, "includeHosts"),
                Exclude = GetStringArrayProperty(root, "excludeHosts")
            };
        }

        // pluginsDir + includePlugins/excludePlugins → "libraries" group with action "pack"
        var pluginsDir = GetStringProperty(root, "pluginsDir");
        if (pluginsDir != null)
        {
            config.ProjectGroups["libraries"] = new ProjectGroup
            {
                SourceDir = pluginsDir,
                Action = "pack",
                Include = GetStringArrayProperty(root, "includePlugins"),
                Exclude = GetStringArrayProperty(root, "excludePlugins")
            };
        }

        // contractsDir + includeContracts/excludeContracts → "contracts" group with action "pack"
        var contractsDir = GetStringProperty(root, "contractsDir");
        if (contractsDir != null)
        {
            config.ProjectGroups["contracts"] = new ProjectGroup
            {
                SourceDir = contractsDir,
                Action = "pack",
                Include = GetStringArrayProperty(root, "includeContracts"),
                Exclude = GetStringArrayProperty(root, "excludeContracts")
            };
        }

        // Preserve nativeBuild if present
        if (root.TryGetProperty("nativeBuild", out var nativeBuildEl) ||
            root.TryGetProperty("NativeBuild", out nativeBuildEl))
        {
            var nbJson = nativeBuildEl.GetRawText();
            var nbOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            config.NativeBuild = JsonSerializer.Deserialize<NativeBuildConfig>(nbJson, nbOptions);
        }

        // Preserve unityBuild if present
        if (root.TryGetProperty("unityBuild", out var unityBuildEl) ||
            root.TryGetProperty("UnityBuild", out unityBuildEl))
        {
            var ubJson = unityBuildEl.GetRawText();
            var ubOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            config.UnityBuild = JsonSerializer.Deserialize<UnityBuildJsonConfig>(ubJson, ubOptions);
        }

        // If no groups were created, the config had no v1 directory properties
        if (config.ProjectGroups.Count == 0)
            config.ProjectGroups = null;

        return config;
    }

    /// <summary>
    /// Generates a list of human-readable changes made during migration.
    /// </summary>
    private static List<string> GetMigrationChanges(JsonDocument doc)
    {
        var changes = new List<string>();
        var root = doc.RootElement;

        var hostsDir = GetStringProperty(root, "hostsDir");
        if (hostsDir != null)
            changes.Add($"Migrated hostsDir '{hostsDir}' → projectGroups.executables (action: publish)");

        var pluginsDir = GetStringProperty(root, "pluginsDir");
        if (pluginsDir != null)
            changes.Add($"Migrated pluginsDir '{pluginsDir}' → projectGroups.libraries (action: pack)");

        var contractsDir = GetStringProperty(root, "contractsDir");
        if (contractsDir != null)
            changes.Add($"Migrated contractsDir '{contractsDir}' → projectGroups.contracts (action: pack)");

        if (GetStringArrayProperty(root, "includeHosts")?.Length > 0)
            changes.Add("Migrated includeHosts → projectGroups.executables.include");
        if (GetStringArrayProperty(root, "excludeHosts")?.Length > 0)
            changes.Add("Migrated excludeHosts → projectGroups.executables.exclude");
        if (GetStringArrayProperty(root, "includePlugins")?.Length > 0)
            changes.Add("Migrated includePlugins → projectGroups.libraries.include");
        if (GetStringArrayProperty(root, "excludePlugins")?.Length > 0)
            changes.Add("Migrated excludePlugins → projectGroups.libraries.exclude");
        if (GetStringArrayProperty(root, "includeContracts")?.Length > 0)
            changes.Add("Migrated includeContracts → projectGroups.contracts.include");
        if (GetStringArrayProperty(root, "excludeContracts")?.Length > 0)
            changes.Add("Migrated excludeContracts → projectGroups.contracts.exclude");

        return changes;
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        // Try camelCase first, then PascalCase
        if (element.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.String)
            return val.GetString();

        var pascalCase = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
        if (element.TryGetProperty(pascalCase, out val) && val.ValueKind == JsonValueKind.String)
            return val.GetString();

        return null;
    }

    private static bool TryGetBoolProperty(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        if (element.TryGetProperty(propertyName, out var val))
        {
            if (val.ValueKind == JsonValueKind.True) { value = true; return true; }
            if (val.ValueKind == JsonValueKind.False) { value = false; return true; }
        }

        var pascalCase = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
        if (element.TryGetProperty(pascalCase, out val))
        {
            if (val.ValueKind == JsonValueKind.True) { value = true; return true; }
            if (val.ValueKind == JsonValueKind.False) { value = false; return true; }
        }

        return false;
    }

    private static string[]? GetStringArrayProperty(JsonElement element, string propertyName)
    {
        JsonElement val;
        if (!element.TryGetProperty(propertyName, out val))
        {
            var pascalCase = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
            if (!element.TryGetProperty(pascalCase, out val))
                return null;
        }

        if (val.ValueKind != JsonValueKind.Array)
            return null;

        return val.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!)
            .ToArray();
    }
}
