# Progress: Dotnet Tool Migration (unify-build)

## Objective

Make `UnifyBuild.Tool` (a `dotnet tool`) the **primary** way to run build and publish targets across repos, instead of relying on the PowerShell/bootstrapped NUKE scripts (`build/nuke/build.ps1`, `build.cmd`, `build.sh`).

Scope:

- Prefer `dotnet tool` install/restore + `dotnet unify-build <Target>` (or `dotnet tool run unify-build <Target>`) for day-to-day usage.
- Keep NUKE as an implementation detail.
- Preserve existing NUKE bootstrap scripts for backward compatibility until consumers migrate.

## Current State (observed)

### Entry points

- **NUKE bootstrap scripts**
  - `build/nuke/build.cmd` (Windows)
  - `build/nuke/build.ps1` (Windows bootstrapper)
  - `build/nuke/build.sh` (Unix bootstrapper)
  - Behavior: bootstrap/ensure a .NET SDK, build `build/nuke/build/_build.csproj`, then `dotnet run` the NUKE build.

- **.NET tool already exists**
  - Project: `dotnet/src/UnifyBuild.Tool/UnifyBuild.Tool.csproj`
  - `PackAsTool=true`, `ToolCommandName=unify-build`, `PackageId=UnifyBuild.Tool`, `TargetFramework=net8.0`
  - Entry point: `dotnet/src/UnifyBuild.Tool/Build.cs`
    - Finds `build.config.json` by walking up the directory tree.
    - Sets `NUKE_ROOT_DIRECTORY` accordingly.
    - Executes NUKE targets via `Execute<Build>()`.

### Configuration

- `unify-build` uses `build.config.json` as the canonical config contract (per `docs/adrs/adr-0001-build-config-schema-versioning.md`).
- This repo has a sample config at `build/nuke/build/build.config.json` (used for building unify-build itself).

### Evidence the tool approach is already feasible

- `Taskfile.yml` includes a `dogfood:tool` task that:
  - Packs `UnifyBuild.Tool` to a local folder (`build/_artifacts/local/flat`)
  - Installs it as a local tool
  - Runs `dotnet unify-build PackProjects ...`

### Tool manifest status

- This repo already has a local tool manifest at `.config/dotnet-tools.json`.
- It pins:
  - `UnifyBuild.Tool` (`unify-build`)
  - `GitVersion.Tool` (`dotnet-gitversion`)

## Target State

### For consumer repos

The preferred consumer setup becomes:

- Add a tool manifest:
  - `.config/dotnet-tools.json`
- Restore and run:
  - `dotnet tool restore`
  - `dotnet unify-build <Target> [args]`

This removes the need for:

- `build/nuke/build.cmd`
- `build/nuke/build.ps1`
- `build/nuke/build.sh`
- `build/nuke/build/_build.csproj`
- Consumer `Build.cs` boilerplate

### For unify-build repo itself

- Keep existing NUKE scripts as legacy entrypoints (they’re still useful for packaging the tool in a clean environment).
- Promote tool usage in docs + Taskfile as the default developer workflow.

## Migration Plan

### Phase 1 — Documentation + canonical commands (this doc)

- Document the intended dotnet tool workflow and the exact commands.
- Identify which existing entrypoints remain supported vs deprecated.

### Phase 2 — Provide a first-class tool manifest workflow in this repo

- Ensure `.config/dotnet-tools.json` exists in `unify-build` (and recommend copying to consumers).
- Update `README.md` to recommend tool-based invocation.
- Update `Taskfile.yml` to provide tool-first tasks.

### Phase 3 — Consumer migration

In each consumer repo:

- Add `.config/dotnet-tools.json` referencing `UnifyBuild.Tool`.
- Ensure the repo has a `build/build.config.json` (or root `build.config.json`) compatible with `BuildContextLoader`.
- Replace CI/build docs + scripts to run:
  - `dotnet tool restore`
  - `dotnet unify-build <Target>`

Optionally:

- Keep `build/nuke` scripts for a transition period.
- Later remove the duplicated consumer boilerplate entirely.

## Proposed Canonical CLI Surface

### Install (local, recommended)

From repo root:

```powershell
# one-time
dotnet new tool-manifest

# pin to a version
dotnet tool install UnifyBuild.Tool --local --version <VERSION>

# run
dotnet unify-build --help
```

### Restore (CI-friendly)

```powershell
dotnet tool restore
```

### Run targets

Examples:

```powershell
# build
dotnet unify-build Compile --configuration Release

# pack nuget packages
dotnet unify-build PackProjects --configuration Release

# publish hosts/plugins and sync latest artifacts
dotnet unify-build SyncLatestArtifacts --configuration Release
```

Notes:

- Target names are NUKE target names exposed by the composed interfaces (e.g., `IUnify`, `IUnifyPublish`, `IUnifyPack`).
- Tool resolves `build.config.json` by searching upward from the current working directory.

## Risks / Watch-outs

- **Dotnet SDK bootstrap**: legacy NUKE scripts will download an SDK if needed. The tool approach assumes `dotnet` is already available in the environment.
- **NuGet feed**: consumers must be able to restore `UnifyBuild.Tool` from their configured feeds (NuGet.org, internal feed, etc.).
- **Version pinning**: tool manifest should pin versions to avoid breaking changes.
- **Nuke compatibility**: `UnifyBuild.Tool` currently enables unsafe BinaryFormatter for NUKE compatibility on .NET 8.

## Acceptance Criteria

- A clean consumer repo can:
  - `dotnet tool restore`
  - `dotnet unify-build PackProjects`
  - `dotnet unify-build PublishHosts` / `PublishPlugins` (as configured)
  - `dotnet unify-build SyncLatestArtifacts`
  - without relying on PowerShell bootstrap scripts.

- `unify-build` repo:
  - Documents tool-first usage.
  - Still supports legacy NUKE scripts (no hard break).

## Current Status

- **Done**
  - Tool exists (`UnifyBuild.Tool`) and has a working dogfood path.
  - RFCs exist describing tool distribution (`docs/rfcs/rfc-0002-dotnet-tool-distribution.md`).
  - `Taskfile.yml` default build tasks now invoke the tool (`dotnet tool restore` + `dotnet tool run unify-build -- ...`).
  - `README.md` documents the tool-first workflow.

- **Next**
  - Migrate consumer repos to tool manifests + tool-first invocations.
  - Optionally deprecate (but keep) legacy `build/nuke` bootstrappers once consumers have migrated.
