# UnifyBuild

Reusable build tooling (NUKE) used across GiantCroissant-Lunar repos.

UnifyBuild is a .NET build orchestration system built on [NUKE](https://nuke.build/) that provides composable component interfaces for building, packing, and publishing .NET, native (CMake), and Unity projects — all driven by a single `build.config.json` configuration file.

## Getting Started

### Install

Add the CLI tool to your repository:

```bash
dotnet new tool-manifest
dotnet tool install UnifyBuild.Tool
```

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
- [Troubleshooting](docs/troubleshooting.md) — common errors and fixes
- [Examples](docs/examples/) — end-to-end project examples
