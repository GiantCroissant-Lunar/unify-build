---
doc_id: RFC-2025-00002
title: "Distribute UnifyBuild as a .NET Tool with Component Interfaces"
doc_type: rfc
status: draft
canonical: true
created: 2026-02-01
tags:
  - build
  - architecture
  - distribution
  - dotnet-tool
  - nuke-components
summary: "Refactor UnifyBuild.Nuke from an abstract base class to composable Nuke component interfaces, and package the result as a .NET tool so consumer projects can run build targets without their own Nuke build project."
owner: TBD
related:
  - RFC-2025-00001
---

# RFC 0002 – Distribute UnifyBuild as a .NET Tool with Component Interfaces

## Context

### Problem 1: Consumer Boilerplate

Consumer projects that use `UnifyBuild.Nuke` today must create a full Nuke build project to leverage the shared build targets. For a typical consumer, this means maintaining:

1. `build/nuke/build/_build.csproj` — references `UnifyBuild.Nuke` as a NuGet dependency
2. `build/nuke/build/Build.cs` — inherits `UnifyBuildBase`, wires `BuildContext`, and calls `Execute<Build>`
3. `build/nuke/build.cmd` / `build.sh` — Nuke bootstrapper scripts
4. `build/nuke/build/Directory.Build.props` — MSBuild isolation

The `Build.cs` across consumers is nearly identical boilerplate:

```csharp
class Build : UnifyBuildBase
{
    protected override BuildContext Context =>
        BuildContextLoader.FromJson(RootDirectory / ".." / "..", "build.config.json");

    public static int Main() => Execute<Build>(x => x.PackProjects);
}
```

Every consumer repository duplicates this scaffolding. The actual build behavior is entirely driven by `build.config.json` — the code adds no value.

### Problem 2: Abstract Base Class Limits Composition

The exported library exposes build functionality through `UnifyBuildBase`, an abstract class that inherits `NukeBuild`. This has structural problems:

- **Single inheritance**: Consumers must inherit `UnifyBuildBase`, which occupies the one available base class slot. They cannot compose behavior from multiple independent sources.
- **Monolithic**: All targets (Compile, PublishHosts, PublishPlugins, PackContracts, Pack, SyncLatestArtifacts) are bundled in one class, even if a consumer only needs a subset.
- **Inconsistency with Nuke idioms**: Nuke's [recommended approach](https://nuke.build/docs/sharing/build-components/) is composable component interfaces using C# default interface implementations, not abstract base classes.
- **Internal vs. exported mismatch**: The project's own internal build components (`build/nuke/build/Components/`) already follow the interface pattern (`ICompile`, `IPack`, `IPublish`, etc.), while the exported library does not.

### Current Consumer Setup

```
consumer-repo/
├── build/
│   ├── build.config.json          ← the only file that matters
│   ├── nuke/
│   │   ├── build.cmd              ← duplicated boilerplate
│   │   ├── build.sh               ← duplicated boilerplate
│   │   └── build/
│   │       ├── _build.csproj      ← duplicated boilerplate
│   │       ├── Build.cs           ← duplicated boilerplate
│   │       └── Directory.Build.props  ← duplicated boilerplate
│   └── ...
```

## Goals

- **G1**: Refactor `UnifyBuildBase` into composable Nuke component interfaces following the [Nuke build components pattern](https://nuke.build/docs/sharing/build-components/)
- **G2**: Package the components as a .NET tool so consumer repos need only `build.config.json` + tool manifest
- **G3**: Allow advanced consumers to compose individual components (e.g., `IUnifyCompile` + `IUnifyPack`) and add custom targets alongside them
- **G4**: Maintain all existing build targets and configuration-driven behavior
- **G5**: Keep Nuke as an implementation detail for tool consumers — they interact with a stable CLI surface

## Non-Goals

- **N1**: Changing the `build.config.json` schema
- **N2**: Supporting non-.NET projects
- **N3**: Replacing Nuke internally — the tool still uses Nuke under the hood

## Proposed Change

This RFC has two parts: (1) refactor the library to use component interfaces, and (2) add a .NET tool entry point that composes them.

### Part 1: Refactor to Component Interfaces

Replace the monolithic `UnifyBuildBase` abstract class with composable Nuke component interfaces. Each interface owns a single concern and follows the `INukeBuild` pattern with default implementations.

#### Component Interface Design

```csharp
/// Foundation component: loads build.config.json and provides BuildContext.
/// All other components depend on this.
interface IUnifyBuildConfig : INukeBuild
{
    /// Resolved build configuration. Consumers can override resolution logic
    /// via explicit interface implementation.
    BuildContext UnifyConfig => TryGetValue(() => UnifyConfig)
        ?? BuildContextLoader.FromJson((AbsolutePath)RootDirectory, "build.config.json");

    [Parameter("Configuration to build - Default is 'Release'")]
    string Configuration => TryGetValue(() => Configuration) ?? "Release";
}
```

```csharp
/// Compile targets: solution-level and explicit project compilation.
interface IUnifyCompile : IUnifyBuildConfig
{
    Target Compile => _ => _
        .Executes(() =>
        {
            if (UnifyConfig.Solution is null) return;
            DotNetBuild(s => s
                .SetProjectFile(UnifyConfig.Solution)
                .SetConfiguration(Configuration));
        });

    Target CompileProjects => _ => _
        .Executes(() =>
        {
            foreach (var project in UnifyConfig.CompileProjects)
            {
                // ...
            }
        });
}
```

```csharp
/// Publish targets: hosts, plugins, and explicit project publishing.
interface IUnifyPublish : IUnifyCompile
{
    Target PublishHosts => _ => _
        .DependsOn<IUnifyCompile>(x => x.Compile)
        .Executes(() => { /* ... */ });

    Target PublishPlugins => _ => _
        .DependsOn<IUnifyCompile>(x => x.Compile)
        .Executes(() => { /* ... */ });

    Target PublishProjects => _ => _
        .DependsOn<IUnifyCompile>(x => x.Compile)
        .Executes(() => { /* ... */ });

    Target SyncLatestArtifacts => _ => _
        .DependsOn<IUnifyPublish>(x => x.PublishHosts)
        .DependsOn<IUnifyPublish>(x => x.PublishPlugins)
        .Executes(() => { /* ... */ });
}
```

```csharp
/// Pack targets: NuGet packaging for contracts, libraries, and explicit projects.
interface IUnifyPack : IUnifyCompile
{
    Target PackContracts => _ => _
        .DependsOn<IUnifyCompile>(x => x.Compile)
        .Executes(() => { /* ... */ });

    Target Pack => _ => _
        .DependsOn<IUnifyCompile>(x => x.Compile)
        .Executes(() => { /* ... */ });

    Target PackProjects => _ => _
        .DependsOn<IUnifyPack>(x => x.Pack);

    Target PackAll => _ => _
        .DependsOn<IUnifyPack>(x => x.PackContracts)
        .DependsOn<IUnifyPack>(x => x.Pack)
        .Executes(() => { /* ... */ });
}
```

#### Component Hierarchy

```
INukeBuild
└── IUnifyBuildConfig          (config loading, Configuration parameter)
    └── IUnifyCompile          (Compile, CompileProjects)
        ├── IUnifyPublish      (PublishHosts, PublishPlugins, PublishProjects, SyncLatestArtifacts)
        └── IUnifyPack         (PackContracts, Pack, PackProjects, PackAll)
```

Each interface is independently usable. A consumer that only needs packing can implement `IUnifyPack` without pulling in publish targets.

#### `IUnify` Convenience Interface

For consumers that want everything:

```csharp
/// Convenience interface that composes all UnifyBuild components.
/// Equivalent to the old UnifyBuildBase but as an interface.
interface IUnify : IUnifyPublish, IUnifyPack { }
```

#### Consumer Usage (Library Approach)

**Minimal — only packing:**
```csharp
class Build : NukeBuild, IUnifyPack
{
    public static int Main() => Execute<Build>(x => ((IUnifyPack)x).PackProjects);
}
```

**Full — all targets:**
```csharp
class Build : NukeBuild, IUnify
{
    public static int Main() => Execute<Build>(x => ((IUnifyPack)x).PackProjects);
}
```

**Extended — custom targets alongside components:**
```csharp
class Build : NukeBuild, IUnifyPack, IUnifyPublish
{
    // Custom target that depends on a component target
    Target Deploy => _ => _
        .DependsOn<IUnifyPublish>(x => x.PublishHosts)
        .Executes(() =>
        {
            // custom deployment logic
        });

    // Override pack settings for this repo
    Target IUnifyPack.Pack => _ => _
        .Inherit<IUnifyPack>()
        .Executes(() =>
        {
            // additional post-pack steps
        });

    public static int Main() => Execute<Build>(x => x.Deploy);
}
```

This is the key advantage over `UnifyBuildBase`: consumers can compose, extend, and override individual targets using Nuke's standard patterns.

### Part 2: .NET Tool Entry Point

Add a new project (`src/UnifyBuild.Tool`) that composes all component interfaces into a single `NukeBuild` class and packages it as a .NET tool.

#### New Project Structure

```
src/
├── UnifyBuild.Nuke/           ← library: component interfaces + config loader
│   ├── UnifyBuild.Nuke.csproj
│   ├── BuildContext.cs
│   ├── BuildConfigJson.cs
│   ├── IUnifyBuildConfig.cs
│   ├── IUnifyCompile.cs
│   ├── IUnifyPublish.cs
│   ├── IUnifyPack.cs
│   └── IUnify.cs              ← convenience interface
│
└── UnifyBuild.Tool/           ← CLI entry point
    ├── UnifyBuild.Tool.csproj
    └── Build.cs
```

#### UnifyBuild.Tool.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>

    <PackAsTool>true</PackAsTool>
    <ToolCommandName>unify-build</ToolCommandName>
    <PackageId>UnifyBuild.Tool</PackageId>
    <Version>3.0.0</Version>
    <Authors>plate</Authors>
    <Description>CLI tool for running UnifyBuild targets from build.config.json without a Nuke build project.</Description>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\UnifyBuild.Nuke\UnifyBuild.Nuke.csproj" />
  </ItemGroup>
</Project>
```

#### Build.cs (Tool Entry Point)

The tool composes all components via `IUnify`:

```csharp
using Nuke.Common;
using UnifyBuild.Nuke;

class Build : NukeBuild, IUnify
{
    public static int Main() => Execute<Build>();
}
```

Root directory resolution needs consideration (see Open Questions).

#### Consumer Setup After Migration

```
consumer-repo/
├── .config/
│   └── dotnet-tools.json          ← pins unify-build version
├── build/
│   └── build.config.json          ← same config as today
```

Consumer invocation:

```bash
# One-time setup
dotnet tool restore

# Run targets
dotnet unify-build PackProjects
dotnet unify-build PublishHosts
dotnet unify-build Compile
```

Or via Taskfile / CI scripts that wrap `dotnet unify-build <target>`.

## Distribution

The tool is distributed as a .NET tool package (`UnifyBuild.Tool`), installable via:

```bash
# Local tool (recommended — version pinned per repo)
dotnet tool install UnifyBuild.Tool --version 3.0.0

# Global tool (for convenience / CI agents)
dotnet tool install -g UnifyBuild.Tool
```

Consumer repositories should use **local tool manifests** (`.config/dotnet-tools.json`) to pin the version:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "unifybuild.tool": {
      "version": "3.0.0",
      "commands": ["unify-build"]
    }
  }
}
```

## Migration Strategy

### Phase 1: Refactor Library to Component Interfaces

1. Create component interfaces (`IUnifyBuildConfig`, `IUnifyCompile`, `IUnifyPublish`, `IUnifyPack`) in `src/UnifyBuild.Nuke/`
2. Move target logic from `UnifyBuildBase` into the corresponding interfaces with default implementations
3. Create `IUnify` convenience interface composing all components
4. Deprecate `UnifyBuildBase` — keep it temporarily as a thin wrapper implementing `IUnify` for backward compatibility:
   ```csharp
   [Obsolete("Use IUnify or individual component interfaces instead.")]
   public abstract class UnifyBuildBase : NukeBuild, IUnify { }
   ```
5. Update unify-build's own `Build.cs` to use `IUnify` instead of `UnifyBuildBase`

### Phase 2: Add Tool Project

1. Create `src/UnifyBuild.Tool/` with `Build : NukeBuild, IUnify`
2. Add pack configuration for the tool in `build.config.json` (`projectGroups`)
3. Validate the tool works against unify-build's own `build.config.json` (dogfooding)
4. Publish both `UnifyBuild.Nuke` (v3.0.0) and `UnifyBuild.Tool` (v3.0.0)

### Phase 3: Consumer Migration

Consumers have two migration paths:

**Path A — Tool (zero-code, recommended for most repos):**
1. Add `.config/dotnet-tools.json` with `UnifyBuild.Tool`
2. Update Taskfile / CI to use `dotnet unify-build <target>`
3. Delete `build/nuke/build/` entirely
4. Keep `build/build.config.json` as-is

**Path B — Library with components (for repos needing custom targets):**
1. Update `Build.cs` to implement component interfaces instead of inheriting `UnifyBuildBase`:
   ```csharp
   // Before
   class Build : UnifyBuildBase { ... }

   // After
   class Build : NukeBuild, IUnifyPack, IUnifyPublish
   {
       Target Deploy => _ => _
           .DependsOn<IUnifyPublish>(x => x.PublishHosts)
           .Executes(() => { /* ... */ });
   }
   ```
2. Remove `UnifyBuildBase` dependency

### Phase 4: Remove UnifyBuildBase

1. Remove the deprecated `UnifyBuildBase` class
2. Update documentation to show component interfaces as the only library approach
3. Release as next major version if Phase 1 kept backward compatibility via the deprecated wrapper

## Trade-offs

### Benefits

- **Composability**: consumers pick only the components they need, and can mix in their own targets — follows the [single-responsibility principle](https://en.wikipedia.org/wiki/Single-responsibility_principle)
- **Nuke-idiomatic**: aligns with Nuke's [official component pattern](https://nuke.build/docs/sharing/build-components/) — consumers familiar with Nuke will recognize the approach
- **Extensible and overridable**: individual targets can be extended (`.Inherit<IUnifyPack>()`) or replaced entirely via explicit interface implementation, without touching the library
- **Eliminates boilerplate**: tool consumers drop from ~5 build files to 1 config + 1 tool manifest
- **Centralized updates**: bumping the tool version pulls in all target improvements — no code changes in consumers
- **Lower adoption barrier**: new repos need only `build.config.json` and `dotnet tool restore`
- **Consistent internal/external design**: the exported library now follows the same interface pattern as the internal components

### Costs

- **C# 8.0+ required**: default interface implementations require C# 8.0 / .NET Core 3.0+. Not an issue since the project already targets .NET 8.0.
- **Interface cast syntax**: accessing component targets from `Main()` requires casting (e.g., `((IUnifyPack)x).PackProjects`). Minor ergonomic cost.
- **CLI surface becomes your API**: for tool consumers, target names and parameters are now a contract — changes are breaking
- **Root directory resolution**: Nuke normally infers the repo root from `.nuke` files or the build project location; a tool invoked from an arbitrary directory needs a different resolution strategy
- **Two packages to maintain**: `UnifyBuild.Nuke` (library) and `UnifyBuild.Tool` (CLI) must be versioned and published together
- **`TryGetValue` pattern**: Nuke's workaround for interface properties not supporting fields is functional but less obvious than plain properties in a class

## Open Questions

1. **Root directory resolution**: How should the tool determine the repository root?
   - Option A: Walk up from CWD looking for `build.config.json` (or `build/build.config.json`)
   - Option B: Require an environment variable (`UNIFY_BUILD_ROOT`)
   - Option C: Use `.nuke` marker file convention (Nuke's default)
   - Option D: Accept a `--root` CLI argument
   - Likely answer: Option A as default with Option D as override. This matches how most build tools behave.

2. **Config override in component interface**: Should `IUnifyBuildConfig.UnifyConfig` use `TryGetValue` to allow consumers to override config resolution via Nuke's parameter injection, or should it just call `BuildContextLoader.FromJson` directly?

3. **Interface naming**: `IUnifyCompile` vs `IUnifyBuildCompile` vs `ICompile`. Using `IUnify` prefix avoids collision with Nuke's own interfaces or consumer-defined interfaces. The internal components use unprefixed names (`ICompile`, `IPack`) which is fine because they're not exported.

4. **Target discoverability**: Nuke provides `--list` by default, which would carry over for tool users. Invocation: `dotnet unify-build --list`

5. **Nuke parameter forwarding**: Nuke targets accept parameters (e.g., `--configuration Release`). These should pass through transparently since the tool is still a `NukeBuild` class.

6. **Versioning alignment**: Should `UnifyBuild.Tool` and `UnifyBuild.Nuke` share the same version number? Sharing versions is simpler to reason about but may force version bumps on the library when only the tool changes (or vice versa).

7. **Backward compatibility duration**: How long should the deprecated `UnifyBuildBase` wrapper remain before removal? One major version cycle seems reasonable.

## Affected Projects

**Primary:**
- `unify-build` (this repository) — refactored library, new tool project, updated `build.config.json`

**Downstream (consumers):**
- `mung-bean` — migrate to tool or component interfaces
- `mung-bean-console` — same
- `mung-bean-window` — same
- `mung-bean-content` — same
- Any future repos — start with tool-only setup

## Success Criteria

- `UnifyBuildBase` is replaced by component interfaces with equivalent behavior
- A new consumer repo can run `dotnet unify-build PackProjects` with only `build.config.json` and a tool manifest
- An advanced consumer can compose `IUnifyPack` + custom targets without inheriting a base class
- Existing `build.config.json` files work without changes
- unify-build can dogfood the tool to build and pack itself

## References

- [Nuke Build Components](https://nuke.build/docs/sharing/build-components/) — official documentation on the interface-based component pattern
- [.NET Tool documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools)
- RFC-0001: Generic Build Configuration Schema
- ADR-0001: Build config schema is unversioned
- Internal components: `build/nuke/build/Components/` (ICompile, IPack, IPublish, etc.)
