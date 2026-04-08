# UnifyBuild

Reusable build tooling (NUKE) used across GiantCroissant-Lunar repos.

UnifyBuild is a .NET build orchestration system built on [NUKE](https://nuke.build/) that provides composable component interfaces for building, packing, and publishing .NET, native (CMake), and Unity projects — all driven by a single `build.config.json` configuration file.

## Distribution Channels

UnifyBuild is organized around three public artifacts with distinct distribution channels:

- `UnifyBuild.Nuke` is the reusable foundation package published to NuGet for config loading, component interfaces, validation, and shared build conventions.
- `UnifyBuild.Tool` is the NuGet-distributed CLI entrypoint layered on top of `UnifyBuild.Nuke`.
- `com.unifybuild.editor` is the Unity-side package under `unity/com.unifybuild.editor`, versioned separately from the NuGet packages and prepared as a standalone UPM/OpenUPM artifact.

The .NET tool still owns Unity orchestration targets such as batch-mode export. The Unity package provides the editor-side `-executeMethod` entrypoints those targets invoke.

For releases, the repository uses a single version line across public artifacts. Release tags, NuGet package versions, and the Unity package manifest should match so consumers and maintainers see one coherent version story.

## Getting Started

### Install

Add the CLI tool to your repository:

```bash
dotnet new tool-manifest
dotnet tool install UnifyBuild.Tool
```

If you are integrating with Unity, keep the Unity-side editor assets in the standalone package at `unity/com.unifybuild.editor`. That package is released separately from the NuGet artifacts so Unity consumers do not need the full .NET packaging surface.

If you are working in this repository on an unpublished version line such as `0.3.x`, bootstrap the local tool feed first:

```bash
task tool:bootstrap-local
dotnet tool restore
```

That packs `UnifyBuild.Nuke` and `UnifyBuild.Tool` into `build/_artifacts/local/flat` and lets the local tool manifest restore against the repo-local `NuGet.config`.

### Initialize

Generate a `build.config.json` from your existing projects:

```bash
dotnet unify-build init
```

Or use a template:

```bash
dotnet unify-build init --template library
dotnet unify-build init --template application
```

### Build

```bash
dotnet tool restore
dotnet unify-build Compile
dotnet unify-build PackProjects --configuration Release
dotnet unify-build PublishHosts --configuration Release
```

### Minimal Configuration

```json
{
  "$schema": "./build.config.schema.json",
  "projectGroups": {
    "packages": {
      "sourceDir": "src",
      "action": "pack",
      "include": ["MyLibrary"]
    }
  }
}
```

`projectGroups` is required. Each group defines a `sourceDir`, an `action` (`compile`, `pack`, or `publish`), and optional `include`/`exclude` filters.

## Commands

| Command | Description |
|---------|-------------|
| `dotnet unify-build init` | Scaffold a new `build.config.json` |
| `dotnet unify-build Compile` | Compile all configured projects |
| `dotnet unify-build PackProjects` | Pack NuGet packages |
| `dotnet unify-build PublishHosts` | Publish executables |
| `dotnet unify-build validate` | Validate config against JSON Schema |
| `dotnet unify-build doctor` | Diagnose configuration and environment issues |

## UnifyBuild.Nuke

`UnifyBuild.Nuke` is the core library consumed by NUKE build scripts to:

- Locate and parse build configuration JSON
- Discover `.csproj` files from configured directories
- Provide a unified `BuildContext` used by build targets

It is the foundation NuGet package for the repository's shared build logic. `UnifyBuild.Tool` composes that library into a stable CLI, while Unity-specific editor entrypoints live in the separate `com.unifybuild.editor` package.

### Loading the config in a NUKE build

```csharp
var ctx = BuildContextLoader.FromJson(RepoRoot, "build.config.json");
```

## JSON Schema Support

The `UnifyBuild.Nuke` package includes a JSON schema file (`build.config.schema.json`) that enables IDE autocomplete and validation for your `build.config.json` files.

Add a `$schema` property at the top of your config:

```json
{
  "$schema": "./build.config.schema.json",
  "artifactsVersion": "local",
  "projectGroups": {
    "packages": {
      "sourceDir": "src",
      "action": "pack",
      "include": ["MyProject"]
    }
  }
}
```

The schema file is automatically copied to your project root when you install or update the `UnifyBuild.Nuke` package. Supported editors: VS Code, Visual Studio, JetBrains Rider, and any editor with JSON Schema support.

For a comprehensive example, see [`build/build.config.example.json`](build/build.config.example.json) and [`build/build.config.example.md`](build/build.config.example.md).

## Documentation

- [Getting Started](docs/getting-started.md) — installation, first config, and common commands
- [Configuration Reference](docs/configuration-reference.md) — all `build.config.json` properties
- [Releasing](docs/releasing.md) — bump versions, tag releases, and OpenUPM onboarding
- [Troubleshooting](docs/troubleshooting.md) — common errors and fixes
- [Examples](docs/examples/) — end-to-end project examples

Repository-only dogfooding and fixture projects live under `fixtures/`. Public, documented consumer examples stay under `examples/`.
