# UnifyBuild Architecture

## Overview

UnifyBuild is a .NET build orchestration system built on [NUKE](https://nuke.build). It uses composable **component interfaces** to define build targets and a JSON-driven **BuildContext** to configure them. The CLI tool (`dotnet unify-build`) composes these components into a single `NukeBuild` class.

## Distribution Surfaces

The repository ships three **packaged** artifacts:

| Artifact | Channel | Source |
|---|---|---|
| `UnifyBuild.Nuke` | NuGet | `dotnet/src/UnifyBuild.Nuke` |
| `UnifyBuild.Tool` | NuGet (.NET tool) | `dotnet/src/UnifyBuild.Tool` |
| `com.unifybuild.editor` | UPM / OpenUPM | `unity/com.unifybuild.editor` |

- `UnifyBuild.Nuke` is the foundation package. It contains config loading, runtime models, validation, and reusable build components.
- `UnifyBuild.Tool` is the CLI. It composes a subset of the exported interfaces from `UnifyBuild.Nuke` into the `dotnet unify-build` entrypoint.
- `com.unifybuild.editor` contains editor-side batch-mode entrypoints invoked by the .NET orchestration layer.

This split is intentional: Unity build/export orchestration remains in the .NET layer because it runs outside the Unity editor, while Unity editor automation is packaged separately as a UPM/OpenUPM-friendly asset.

Two additional developer-facing surfaces live in the repository but are not consumed as packages:

- `vscode-extension/` — VS Code extension providing schema-backed IntelliSense, hover docs, snippets, a project-groups tree view, and command-palette access to `init`/`validate`/`doctor`.
- `dashboard/` — a dependency-free single-page app that visualizes the JSON metrics emitted when [`observability.enableMetrics`](../reference/configuration-reference.md#observability-configuration) is on.

```
┌───────────────────────────────────────────────────────────────┐
│                       UnifyBuild.Tool                          │
│                                                                │
│  Build : NukeBuild, IUnify, IUnifyNative, IUnifyUnity,         │
│          IUnifyGodot, IUnifyMobile, IUnifyUnityExport,         │
│          IUnifySchemaGeneration                                │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌─────────┐           │
│  │   Init   │ │ Validate │ │  Doctor  │ │ Migrate │           │
│  └──────────┘ └──────────┘ └──────────┘ └─────────┘           │
└──────────────────────┬────────────────────────────────────────┘
                       │ references
┌──────────────────────▼────────────────────────────────────────┐
│                      UnifyBuild.Nuke                           │
│                                                                │
│  ┌──────────────────────────────────────────────┐             │
│  │           IUnifyBuildConfig (base)            │             │
│  │  ┌───────────────┐  ┌──────────────────────┐ │             │
│  │  │  UnifyConfig  │  │ BuildContextLoader   │ │             │
│  │  │ (BuildContext)│  │ .FromJson()          │ │             │
│  │  └───────────────┘  └──────────────────────┘ │             │
│  └──────────────────────────────────────────────┘             │
│                                                                │
│  .NET ───────────────────────────────────────────┐            │
│  │ IUnifyCompile ─┬─ IUnifyPack  ─┐              │            │
│  │                └─ IUnifyPublish ┴─ IUnify     │            │
│  └───────────────────────────────────────────────┘            │
│  Native ─────────────┐  Engines ─────────────────┐            │
│  │ IUnifyNative      │  │ IUnifyUnity            │            │
│  │ IUnifyRust      * │  │ IUnifyUnityExport      │            │
│  │ IUnifyGo        * │  │ IUnifyGodot            │            │
│  └───────────────────┘  │ IUnifyMobile           │            │
│                         └────────────────────────┘            │
│  IUnifySchemaGeneration                  * library only —     │
│                                            not in the CLI     │
│                                                                │
│  Performance/        Validation/         Commands/             │
│  ├─ChangeDetection   ├─ConfigValidator   ├─InitCommand         │
│  ├─BuildCache        └─ValidationResult  ├─ConfigWizard        │
│  └─BuildMetrics                          ├─DoctorCommand       │
│  PackageManagement/  Telemetry/          ├─ValidateCommand     │
│  Diagnostics/        Extensibility/      └─MigrateCommand      │
└────────────────────────────────────────────────────────────────┘
                       │ reads
┌──────────────────────▼────────────────────────────────────────┐
│                     build.config.json                          │
│      { "$schema": "...", "projectGroups": { ... } }           │
└────────────────────────────────────────────────────────────────┘
```

## Component Interface Pattern

Every build capability is defined as a C# interface that extends `IUnifyBuildConfig`. This is the core extensibility mechanism.

### Base Interface: IUnifyBuildConfig

All components depend on `IUnifyBuildConfig`, which provides access to the resolved `BuildContext`:

```csharp
public interface IUnifyBuildConfig : INukeBuild
{
    BuildContext UnifyConfig { get; }

    [Parameter("Configuration to build - Default is 'Release'")]
    string Configuration => TryGetValue(() => Configuration) ?? "Release";
}
```

The `Build` class implements this by calling `BuildContextLoader.FromJson()`:

```csharp
class Build : NukeBuild, IUnify, IUnifyNative, IUnifyUnity
{
    BuildContext IUnifyBuildConfig.UnifyConfig =>
        BuildContextLoader.FromJson(RootDirectory, "build.config.json");
}
```

### Interface Hierarchy

```
IUnifyBuildConfig (base — provides UnifyConfig)
├── IUnifyCompile ............. Compile, CompileProjects
│   ├── IUnifyPack ............ Pack, PackContracts, PackProjects, PackAll, SyncLocalFeed
│   ├── IUnifyPublish ......... PublishHosts, PublishPlugins, PublishProjects, SyncLatestArtifacts
│   ├── IUnifyUnity ........... BuildForUnity
│   └── IUnifyGodot ........... BuildGodot, BuildGodotDesktop, BuildGodotMobile
├── IUnifyNative .............. Native (CMake)
├── IUnifyRust ................ RustBuild (Cargo)
├── IUnifyGo .................. GoBuild
├── IUnifyMobile .............. MobileRestore, MobileBuild*, MobileDeploy* (Fastlane)
├── IUnifyUnityExport ......... UnityExport, UnityExportDesktop, UnityExportMobile
└── IUnifySchemaGeneration .... GenerateSchema

IUnify : IUnifyPublish, IUnifyPack ... convenience aggregate (no targets of its own)
```

`IUnifyPack` and `IUnifyPublish` are **siblings** — both extend `IUnifyCompile` directly, and neither derives from the other. `IUnify` exists to pull both into a single interface.

Each interface defines one or more NUKE `Target` properties. NUKE discovers and orchestrates these targets automatically. See the [Targets Reference](../reference/targets.md) for the full catalog with dependencies.

### What the CLI composes

The `unify-build` tool does **not** implement every interface:

```csharp
class Build : NukeBuild,
    IUnify, IUnifyNative, IUnifyUnity, IUnifyGodot,
    IUnifySchemaGeneration, IUnifyMobile, IUnifyUnityExport
```

`IUnifyRust` and `IUnifyGo` ship in `UnifyBuild.Nuke` but are not composed into the CLI. Consumers who need `RustBuild` or `GoBuild` implement those interfaces in their own NUKE build class. This is the same extension mechanism used for [custom components](#1-adding-a-new-component-interface).

### Target Composition

Targets can declare dependencies on targets from other interfaces using `DependsOn<T>()`:

```csharp
public interface IUnifyPack : IUnifyCompile
{
    Target Pack => _ => _
        .DependsOn<IUnifyCompile>(x => x.Compile)
        .Executes(() => { /* pack logic */ });
}
```

This means running `Pack` automatically runs `Compile` first. The `Build` class composes all interfaces, and NUKE resolves the full dependency graph at runtime.

## BuildContext and BuildConfigJson

There are two distinct models:

| Model | Role | Location |
|-------|------|----------|
| `BuildJsonConfig` | JSON deserialization model — maps 1:1 to `build.config.json` | `BuildConfigJson.cs` |
| `BuildContext` | Runtime model — resolved paths, computed values | `BuildContext.cs` |

### Data Flow

```
build.config.json
    │
    ▼ (System.Text.Json deserialization)
BuildJsonConfig  (raw strings, nullable fields)
    │
    ▼ (BuildContextLoader.FromJson)
BuildContext     (AbsolutePath, resolved arrays, computed defaults)
```

`BuildContextLoader.FromJson()` handles:
1. Reading and deserializing `build.config.json` into `BuildJsonConfig`
2. Resolving version from environment variables (`GITVERSION_MAJORMINORPATCH`, etc.)
3. Converting relative paths to `AbsolutePath` values
4. Discovering projects in each `ProjectGroup` via `DiscoverProjectsInGroup()`
5. Creating sub-contexts (`NativeBuildContext`, `RustBuildContext`, `GoBuildContext`, `UnityBuildContext`, `UnityExportContext`, `GodotBuildContext`, `MobileBuildContext`)

Several sub-contexts are created by convention even when their config section is absent: a `native/CMakeLists.txt` activates native builds, and a `mobile/` directory activates mobile builds. Output directories default to `build/_artifacts/{version}/<kind>` — `native`, `rust`, `go`, `godot`, `mobile`, `unity-export`.

When Unity export automation is configured, the runtime context bridges the two release surfaces: the .NET side resolves the paths and orchestration data, then invokes editor entrypoints shipped in `com.unifybuild.editor`.

### BuildJsonConfig (Deserialization Model)

Key properties:

```csharp
public sealed class BuildJsonConfig
{
    public string? Version { get; set; }
    public string? VersionEnv { get; set; }
    public string? Solution { get; set; }
    public Dictionary<string, ProjectGroup>? ProjectGroups { get; set; }
    public string[]? CompileProjects { get; set; }
    public string[]? PublishProjects { get; set; }
    public string[]? PackProjects { get; set; }
    public NativeBuildConfig? NativeBuild { get; set; }
    public RustBuildConfig? RustBuild { get; set; }
    public GoBuildConfig? GoBuild { get; set; }
    public UnityBuildJsonConfig? UnityBuild { get; set; }
    public UnityExportConfig? UnityExport { get; set; }
    public GodotBuildConfig? GodotBuild { get; set; }
    public MobileBuildConfig? MobileBuild { get; set; }
    public PackageManagementConfig? PackageManagement { get; set; }
    public PerformanceConfig? Performance { get; set; }
    public ObservabilityConfig? Observability { get; set; }
    public ExtensionsConfig? Extensions { get; set; }
    // ... additional properties
}
```

Each build type has a corresponding config class (e.g., `RustBuildConfig`) that maps to a runtime context record (e.g., `RustBuildContext`). The config classes live in `Models/`; the schema is generated from them by reflection, so adding a property to a model is what makes it appear in `build.config.schema.json`. See the [Configuration Reference](../reference/configuration-reference.md) for the user-facing view of every property.

### BuildContext (Runtime Model)

```csharp
public sealed record BuildContext
{
    public AbsolutePath RepoRoot { get; init; }
    public string[] CompileProjects { get; init; }
    public string[] PublishProjects { get; init; }
    public string[] PackProjects { get; init; }
    public string? Version { get; init; }
    public NativeBuildContext? NativeBuild { get; init; }
    public RustBuildContext? RustBuild { get; init; }
    public GoBuildContext? GoBuild { get; init; }
    public UnityBuildContext? UnityBuild { get; init; }
    public UnityExportContext? UnityExport { get; init; }
    public GodotBuildContext? GodotBuild { get; init; }
    public MobileBuildContext? MobileBuild { get; init; }
    // ... additional properties
}
```

## Extension Points

### 1. Adding a New Component Interface

Create a new interface extending `IUnifyBuildConfig` with one or more `Target` properties. See [Extending UnifyBuild](../guides/extending.md) for a step-by-step walkthrough.

### 2. Adding New Config Properties

Extend `BuildJsonConfig` with a new config class, add a corresponding runtime context record, and wire the mapping in `BuildContextLoader`. The JSON schema (`build.config.schema.json`) should also be updated.

### 3. Adding New CLI Commands

Create a command class in `Commands/` and wire it as a `Target` in `Build.cs`. Commands follow the pattern of `InitCommand`, `ValidateCommand`, `DoctorCommand`, and `MigrateCommand`.

### 4. Performance Extensions

The `Performance/` namespace contains `ChangeDetection`, `BuildCache`, and `BuildMetrics`. These can be extended to add new caching strategies, metrics exporters, or change detection algorithms.

### 5. Validation Extensions

`ConfigValidator` performs schema and semantic validation. New validation rules can be added by extending `ValidateSemantic()` or adding new check methods to `DoctorCommand`.

## Project Structure

```
dotnet/
├── src/
│   ├── UnifyBuild.Nuke/              # Core library (NuGet package)
│   │   ├── IUnifyBuildConfig.cs       # Base interface
│   │   ├── IUnifyCompile.cs           # Compile targets
│   │   ├── IUnifyPack.cs              # Pack targets
│   │   ├── IUnifyPublish.cs           # Publish targets
│   │   ├── IUnify.cs                  # Convenience aggregate
│   │   ├── IUnifyNative.cs            # CMake builds
│   │   ├── IUnifyRust.cs              # Cargo builds
│   │   ├── IUnifyGo.cs                # Go builds
│   │   ├── IUnifyUnity.cs             # Unity package DLL staging
│   │   ├── IUnifyUnityExport.cs       # Unity player export
│   │   ├── IUnifyGodot.cs             # Godot export
│   │   ├── IUnifyMobile.cs            # Fastlane build + deploy
│   │   ├── IUnifySchemaGeneration.cs  # Schema generation
│   │   ├── BuildContext.cs            # Runtime config model
│   │   ├── BuildConfigJson.cs         # JSON model (BuildJsonConfig)
│   │   ├── BuildContextLoader.cs      # JSON → BuildContext resolution
│   │   ├── BuildConfigSchemaGenerator.cs  # Reflection-based schema emit
│   │   ├── *BuildContext.cs           # Runtime sub-contexts per build kind
│   │   ├── Models/                    # JSON config classes (schema source)
│   │   ├── Commands/                  # CLI commands + config wizard
│   │   ├── Diagnostics/               # Error codes and messages
│   │   ├── Validation/                # Config validation
│   │   ├── Performance/               # Caching, metrics, change detection
│   │   ├── PackageManagement/         # Registries, signing, SBOM, retention
│   │   ├── Telemetry/                 # Local opt-in telemetry
│   │   └── Extensibility/             # Plugin assembly loading
│   └── UnifyBuild.Tool/              # CLI tool (dotnet unify-build)
│       └── Build.cs                   # Composes components + command targets
├── tests/
│   ├── UnifyBuild.Nuke.Tests/         # Unit + property tests
│   ├── UnifyBuild.Integration.Tests/  # End-to-end tests
│   └── UnifyBuild.Package.Tests/      # Packaging / distribution tests
└── UnifyBuild.sln

unity/com.unifybuild.editor/           # Unity editor entrypoints (UPM)
vscode-extension/                      # VS Code extension
dashboard/                             # Build metrics dashboard (static SPA)
examples/                              # Documented consumer examples
fixtures/                              # Repo-internal dogfooding fixtures
```
