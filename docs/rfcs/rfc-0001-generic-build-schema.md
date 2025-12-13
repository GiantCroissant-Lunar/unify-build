---
doc_id: RFC-2025-00001
title: "Generic Build Configuration Schema"
doc_type: rfc
status: draft
canonical: true
created: 2025-12-13
tags:
  - build
  - architecture
  - schema
  - refactoring
summary: "Refactor BuildJsonConfig from domain-specific terminology (hosts, plugins, contracts) to generic build concepts (executables, libraries, packages) to make unify-build reusable across different project architectures."
owner: TBD
related: []
---

# RFC 0001 – Generic Build Configuration Schema

## Context

UnifyBuild is a shared build orchestration system built on NUKE. Currently, the build configuration schema (`BuildJsonConfig` in `src/UnifyBuild.Nuke/BuildConfigJson.cs`) uses domain-specific terminology from plugin-based architectures:

- `HostsDir` / `IncludeHosts` / `ExcludeHosts` - for executable applications
- `PluginsDir` / `IncludePlugins` / `ExcludePlugins` - for loadable plugins
- `ContractsDir` / `IncludeContracts` / `ExcludeContracts` - for abstraction packages

**Problem**: These terms are specific to the MungBean plugin architecture. A build system should be **domain-agnostic** and work for any .NET project structure (monorepo, microservices, libraries, etc.).

### Architectural Inconsistency

UnifyBuild currently has **two different build systems**:

1. **Internal Components** (`build/nuke/build/Components/`)
   - Generic, reusable NUKE components
   - Schema: `solutionPath`, `sourceDir`, `frameworkDirs`, `pluginDirs`, `publishProjectPaths`
   - Used by unify-build to build itself
   - Design philosophy: "generic and configured via build.config.json so they can be reused across projects"

2. **Exported Package** (`src/UnifyBuild.Nuke/`)
   - NuGet package v0.1.7: `UnifyBuild.Nuke`
   - Schema: Domain-specific (HostsDir, PluginsDir, ContractsDir)
   - Used by MungBean* consumer projects
   - Design: Tailored for plugin-based architectures

**The Irony**: The internal build system is MORE generic than the exported package!

### The Dogfooding Test

**Current state**: unify-build CANNOT dogfood itself using UnifyBuild.Nuke because:
- unify-build is a simple library project (`src/UnifyBuild.Nuke`)
- The exported schema forces "hosts/plugins/contracts" terminology
- No natural way to express "pack this library" without abusing "contractsDir"

This validates that the schema is too domain-specific and needs refactoring.

## Current Schema (BuildJsonConfig.cs:8-66)

```csharp
public sealed class BuildJsonConfig
{
    public string HostsDir { get; set; } = "project/hosts";
    public string PluginsDir { get; set; } = "project/plugins";
    public string? Solution { get; set; } = "project/MungBean.SkiaSharp.sln";
    public string? ContractsDir { get; set; }
    public string? NuGetOutputDir { get; set; }
    public string? Version { get; set; }
    public string? ArtifactsVersion { get; set; }
    public string? VersionEnv { get; set; } = "Version";
    public string[]? IncludeHosts { get; set; }
    public string[]? ExcludeHosts { get; set; }
    public string[]? IncludePlugins { get; set; }
    public string[]? ExcludePlugins { get; set; }
    public string[]? IncludeContracts { get; set; }
    public string[]? ExcludeContracts { get; set; }
    public string[]? CompileProjects { get; set; }
    public Dictionary<string, string>? PackProperties { get; set; }
}
```

## Goals

- **G1**: Enable unify-build to dogfood itself (build using its own exported package)
- **G2**: Replace domain-specific terminology with generic build concepts
- **G3**: Support multiple project groups with different build actions (publish, pack, compile-only)
- **G4**: Maintain backward compatibility during migration period
- **G5**: Make unify-build reusable for non-plugin-based .NET projects (microservices, monorepos, libraries)
- **G6**: Improve clarity: "what to build" vs "what architectural pattern is used"

## Non-Goals

- **N1**: Changing NUKE build infrastructure itself
- **N2**: Modifying existing published packages or artifacts structure
- **N3**: Supporting non-.NET build systems

## Proposed Schema v2

### New BuildJsonConfig Structure

```csharp
public sealed class BuildJsonConfig
{
    // Version information
    public string? Version { get; set; }
    public string? VersionEnv { get; set; } = "Version";
    public string? ArtifactsVersion { get; set; }

    // Solution (optional)
    public string? Solution { get; set; }

    // Generic project groups - replaces domain-specific dirs
    public Dictionary<string, ProjectGroup>? ProjectGroups { get; set; }

    // Explicit project paths for edge cases
    public string[]? CompileProjects { get; set; }
    public string[]? PublishProjects { get; set; }
    public string[]? PackProjects { get; set; }

    // Output configuration
    public string? NuGetOutputDir { get; set; }
    public string? PublishOutputDir { get; set; }

    // MSBuild properties for pack operations
    public Dictionary<string, string>? PackProperties { get; set; }

    // Local NuGet feed sync (existing)
    public bool SyncLocalNugetFeed { get; set; } = false;
    public string? LocalNugetFeedRoot { get; set; }
    public string? LocalNugetFeedFlatSubdir { get; set; } = "flat";
    public string? LocalNugetFeedHierarchicalSubdir { get; set; } = "hierarchical";
    public string? LocalNugetFeedBaseUrl { get; set; }
}

public sealed class ProjectGroup
{
    /// <summary>
    /// Directory containing projects for this group (e.g., "project/apps", "project/libs")
    /// </summary>
    public string SourceDir { get; set; } = string.Empty;

    /// <summary>
    /// Build action: "publish" (executables), "pack" (NuGet packages), "compile" (build only)
    /// </summary>
    public string Action { get; set; } = "compile";

    /// <summary>
    /// Project names to include (without .csproj). If empty, all projects in SourceDir are included.
    /// </summary>
    public string[]? Include { get; set; }

    /// <summary>
    /// Project names to exclude (without .csproj)
    /// </summary>
    public string[]? Exclude { get; set; }

    /// <summary>
    /// Optional: Override output directory for this group
    /// </summary>
    public string? OutputDir { get; set; }
}
```

### Example v2 Configuration

**Generic multi-tier application:**
```json
{
  "version": null,
  "versionEnv": "Version",
  "artifactsVersion": "1.0.0",
  "solution": "MySolution.sln",

  "projectGroups": {
    "apis": {
      "sourceDir": "src/apis",
      "action": "publish",
      "include": ["UserApi", "PaymentApi"],
      "exclude": []
    },
    "workers": {
      "sourceDir": "src/workers",
      "action": "publish",
      "exclude": ["LegacyWorker"]
    },
    "libraries": {
      "sourceDir": "src/libs",
      "action": "pack",
      "exclude": ["Internal.TestHelpers"]
    }
  },

  "packProperties": {
    "PackageProjectUrl": "https://github.com/org/repo"
  }
}
```

**MungBean-style plugin architecture (backward compatible semantics):**
```json
{
  "versionEnv": "Version",
  "artifactsVersion": "console",

  "projectGroups": {
    "executables": {
      "sourceDir": "project/hosts",
      "action": "publish",
      "include": ["MungBean.Console.TerminalGui"]
    },
    "plugins": {
      "sourceDir": "project/plugins",
      "action": "publish",
      "exclude": ["MungBean.Hud.TerminalGui.Tests"]
    },
    "contracts": {
      "sourceDir": "project/contracts",
      "action": "pack",
      "exclude": ["MungBean.Inventory.Serialization"]
    }
  },

  "compileProjects": [
    "project/plugins/MungBean.Hud.TerminalGui/MungBean.Hud.TerminalGui.csproj"
  ],

  "packProperties": {
    "UseDevelopmentReferences": "false"
  }
}
```

## Migration Strategy

### Phase 1: Dual Schema Support (v2.0.0)

1. **Add v2 schema alongside v1**
   - Create `BuildJsonConfigV2` and `ProjectGroup` classes
   - Add `BuildContextLoaderV2.FromJson()` method
   - Keep existing `BuildJsonConfig` and `BuildContextLoader` unchanged

2. **Add schema version detection**
   ```csharp
   public static BuildContext FromJson(AbsolutePath repoRoot, string configFile)
   {
       var json = File.ReadAllText(path);

       // Detect schema version
       if (json.Contains("\"projectGroups\""))
           return BuildContextLoaderV2.FromJson(repoRoot, configFile);
       else
           return BuildContextLoaderV1.FromJson(repoRoot, configFile); // legacy
   }
   ```

3. **Update `UnifyBuildBase` to use `BuildContext` from either schema**
   - Ensure `BuildContext` can be constructed from both v1 and v2 configs
   - Internal `BuildContext` representation remains stable

### Phase 2: Migration Period (6 months)

1. **Update documentation** to show v2 as recommended approach
2. **Add deprecation warnings** when v1 schema is detected:
   ```
   [WARN] Build config using legacy schema (HostsDir, PluginsDir).
          Consider migrating to v2 schema (projectGroups).
          See docs/rfcs/rfc-0001-generic-build-schema.md
   ```
3. **Migrate example projects** in unify-build docs to v2 schema
4. **Downstream projects migrate at their own pace**

### Phase 3: v1 Schema Removal (v3.0.0, breaking change)

1. Remove `BuildJsonConfig` (v1) class entirely
2. Rename `BuildJsonConfigV2` → `BuildJsonConfig`
3. Update all documentation
4. Publish v3.0.0 as major version with breaking changes

## Implementation Checklist

### Phase 1: Implement v2 Schema in unify-build

**Core Schema Implementation:**
- [ ] Create `BuildJsonConfigV2` and `ProjectGroup` classes in `src/UnifyBuild.Nuke/BuildConfigJsonV2.cs`
- [ ] Implement `BuildContextLoaderV2.FromJson()` with schema version detection
- [ ] Update `BuildContext` to represent both v1 and v2 configs internally
- [ ] Add JSON deserialization tests for v2 schema

**UnifyBuildBase Updates:**
- [ ] Add support for v2 schema in `UnifyBuildBase` targets
- [ ] Map `ProjectGroup` actions to appropriate build targets (publish/pack/compile)
- [ ] Ensure backward compatibility with existing v1 consumers

**Testing:**
- [ ] Add unit tests for v2 schema parsing and validation
- [ ] Add integration tests showing both schemas work
- [ ] Test edge cases (empty groups, missing fields, invalid actions)

**Documentation:**
- [ ] Update README with v2 schema examples
- [ ] Create migration guide in `docs/migration-v1-to-v2.md`
- [ ] Add deprecation warnings when v1 schema is detected

### Phase 2: Dogfooding Validation (Proves Schema is Generic)

**Prerequisite:** Phase 1 complete, UnifyBuild.Nuke v2.0.0 published to local NuGet feed

**Create v2 Config for unify-build:**
- [ ] Create `build/build.config.v2.json` using v2 schema:
  ```json
  {
    "versionEnv": "BUILD_VERSION",
    "projectGroups": {
      "library": {
        "sourceDir": "src",
        "action": "pack",
        "include": ["UnifyBuild.Nuke"]
      }
    },
    "packProperties": {
      "GeneratePackageOnBuild": "false"
    }
  }
  ```

**Migrate unify-build's Build.cs:**
- [ ] Update `build/nuke/build/_build.csproj` to reference `UnifyBuild.Nuke` v2.0.0 package
- [ ] Rewrite `build/nuke/build/Build.cs` to inherit from `UnifyBuildBase`:
  ```csharp
  class Build : UnifyBuildBase
  {
      protected override BuildContext Context =>
          BuildContextLoader.FromJson(RootDirectory / ".." / "..", "build.config.v2.json");

      public static int Main() => Execute<Build>(x => x.PackProjects);
  }
  ```
- [ ] Remove Component interfaces (IBuildConfig, IClean, etc.) - now provided by UnifyBuildBase
- [ ] Delete or archive `build/nuke/build/Components/` directory

**Validation:**
- [ ] Run `nuke Clean` using new Build.cs - should work
- [ ] Run `nuke PackProjects` - should produce UnifyBuild.Nuke.{version}.nupkg
- [ ] Verify package output in `build/_artifacts/{version}/nuget/`
- [ ] Compare with previous package - should be functionally identical
- [ ] **SUCCESS CRITERION**: unify-build builds itself using its own exported package ✅

### Phase 3: Consumer Migration Support

**For downstream consumers (mung-bean*):**
- [ ] See RFC-0045 in mung-bean repository for consumer migration plan
- [ ] Ensure v1 schema continues to work (backward compatibility)
- [ ] Provide migration examples in documentation

### Phase 4: Deprecation (Future v3.0.0)

**After 6-month migration period:**
- [ ] Remove v1 schema classes (breaking change)
- [ ] Rename `BuildJsonConfigV2` → `BuildJsonConfig`
- [ ] Update all documentation
- [ ] Release as UnifyBuild.Nuke v3.0.0

## Affected Projects

**Primary:**
- `unify-build` (this repository) - schema definition, build engine

**Downstream (consumers):**
- `mung-bean` - main project build configs
- `mung-bean-console` - console variant build configs
- `mung-bean-window` - window variant build configs
- `mung-bean-content` - content build configs (if applicable)

## Rollout Timeline

- **Week 1-2**: Implement v2 schema in unify-build (Phase 1)
- **Week 3**: Test v2 schema with example configs
- **Week 4**: Release unify-build v2.0.0 with dual schema support
- **Months 2-6**: Migration period, downstream projects update configs (Phase 2)
- **Month 7+**: Evaluate v1 schema removal for v3.0.0 (Phase 3)

## Open Questions

1. **Q**: Should we support mixing v1 and v2 schemas in the same config file?
   **A**: No, require full migration to v2 to avoid confusion.

2. **Q**: What happens to existing `BuildContext` consumers if internal structure changes?
   **A**: `BuildContext` remains stable; both v1 and v2 loaders produce compatible `BuildContext` instances.

3. **Q**: Should `projectGroups` be ordered or unordered?
   **A**: Dictionary (unordered). Build order determined by dependency graph, not config order.

4. **Q**: How to handle custom build targets that reference v1 properties like `Context.HostsDir`?
   **A**: Provide compatibility shims in `BuildContext` during migration period that map v2 groups to v1 properties.

## Success Metrics

- ✅ **unify-build can dogfood itself** - Build using its own UnifyBuild.Nuke v2 package (not Components)
- ✅ unify-build can be used in non-plugin-based .NET projects (simple libraries, microservices, etc.)
- ✅ All existing mung-bean* builds work with both v1 (legacy) and v2 schemas
- ✅ Build config clearly separates "what to build" from "architectural patterns"
- ✅ Zero breaking changes for consumers during Phase 1-2
- ✅ Documentation shows v2 as recommended approach

### Dogfooding Validation

Create `build/build.config.v2.json` for unify-build itself:
```json
{
  "versionEnv": "BUILD_VERSION",

  "projectGroups": {
    "library": {
      "sourceDir": "src",
      "action": "pack",
      "include": ["UnifyBuild.Nuke"]
    }
  }
}
```

Then migrate `build/nuke/build/Build.cs` to:
```csharp
class Build : UnifyBuildBase  // Instead of: NukeBuild, IBuildConfig, ...
{
    protected override BuildContext Context =>
        BuildContextLoader.FromJson(RootDirectory, "build.config.v2.json");

    public static int Main() => Execute<Build>(x => x.PackProjects);
}
```

If this works cleanly, the schema is truly generic.

## References

- Current schema: `unify-build/src/UnifyBuild.Nuke/BuildConfigJson.cs`
- Current usage: `mung-bean*/build/nuke/build/Build.cs`
- Related: MungBean RFC-0045 (consumer migration plan)
