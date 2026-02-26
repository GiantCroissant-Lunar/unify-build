# UnifyBuild Architecture

## Overview

UnifyBuild is a .NET build orchestration system built on [NUKE](https://nuke.build). It uses composable **component interfaces** to define build targets and a JSON-driven **BuildContext** to configure them. The CLI tool (`dotnet unify-build`) composes these components into a single `NukeBuild` class.

```
┌─────────────────────────────────────────────────────┐
│                  UnifyBuild.Tool                     │
│                                                      │
│  Build : NukeBuild, IUnify, IUnifyNative, IUnifyUnity│
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌─────────┐│
│  │   Init   │ │ Validate │ │  Doctor  │ │ Migrate ││
│  └──────────┘ └──────────┘ └──────────┘ └─────────┘│
└──────────────────────┬──────────────────────────────┘
                       │ references
┌──────────────────────▼──────────────────────────────┐
│                 UnifyBuild.Nuke                      │
│                                                      │
│  ┌──────────────────────────────────────────────┐   │
│  │           IUnifyBuildConfig (base)            │   │
│  │  ┌─────────────┐  ┌──────────────────────┐   │   │
│  │  │ UnifyConfig  │  │ BuildContextLoader   │   │   │
│  │  │ (BuildContext)│  │ .FromJson()          │   │   │
│  │  └─────────────┘  └──────────────────────┘   │   │
│  └──────────────────────────────────────────────┘   │
│                                                      │
│  ┌────────────┐ ┌──────────┐ ┌───────────────┐     │
│  │IUnifyCompile│ │IUnifyPack│ │IUnifyPublish  │     │
│  └─────┬──────┘ └────┬─────┘ └───────────────┘     │
│        │              │                              │
│  ┌─────┴──────┐ ┌────┴─────┐ ┌──────────────┐      │
│  │IUnifyNative│ │IUnifyRust│ │ IUnifyGo     │      │
│  └────────────┘ └──────────┘ └──────────────┘      │
│  ┌────────────┐                                     │
│  │IUnifyUnity │                                     │
│  └────────────┘                                     │
│                                                      │
│  Performance/        Validation/       Commands/     │
│  ├─ChangeDetection   ├─ConfigValidator  ├─InitCommand│
│  ├─BuildCache         └─ValidationResult ├─DoctorCmd │
│  └─BuildMetrics                         ├─ValidateCmd│
│                                         └─MigrateCmd │
└──────────────────────────────────────────────────────┘
                       │ reads
┌──────────────────────▼──────────────────────────────┐
│              build.config.json                       │
│  { "$schema": "...", "projectGroups": { ... } }     │
└──────────────────────────────────────────────────────┘
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
├── IUnifyCompile (Compile, CompileProjects targets)
│   ├── IUnifyPack : IUnifyCompile (Pack, PackContracts, PackAll targets)
│   │   └── IUnifyPublish : IUnifyPack (Publish targets)
│   │       └── IUnify : IUnifyPublish, IUnifyPack (convenience aggregate)
│   └── IUnifyUnity : IUnifyCompile (BuildForUnity target)
├── IUnifyNative (Native target — CMake builds)
├── IUnifyRust (RustBuild target — Cargo builds)
└── IUnifyGo (GoBuild target — Go builds)
```

Each interface defines one or more NUKE `Target` properties. NUKE discovers and orchestrates these targets automatically.

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
5. Creating sub-contexts (`NativeBuildContext`, `RustBuildContext`, `GoBuildContext`, `UnityBuildContext`)

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
    // ... additional properties
}
```

Each build type has a corresponding config class (e.g., `RustBuildConfig`) that maps to a runtime context record (e.g., `RustBuildContext`).

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
    // ... additional properties
}
```

## Extension Points

### 1. Adding a New Component Interface

Create a new interface extending `IUnifyBuildConfig` with one or more `Target` properties. See [Contributing Guide](contributing.md) for a step-by-step walkthrough.

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
│   │   ├── IUnifyUnity.cs             # Unity builds
│   │   ├── BuildContext.cs            # Runtime config model
│   │   ├── BuildConfigJson.cs         # JSON model + BuildContextLoader
│   │   ├── Commands/                  # CLI commands
│   │   ├── Diagnostics/               # Error codes and messages
│   │   ├── Validation/                # Config validation
│   │   └── Performance/               # Caching, metrics, change detection
│   └── UnifyBuild.Tool/              # CLI tool (dotnet unify-build)
│       └── Build.cs                   # Composes all components
├── tests/
│   ├── UnifyBuild.Nuke.Tests/         # Unit + property tests
│   └── UnifyBuild.Integration.Tests/  # End-to-end tests
└── UnifyBuild.sln
```
