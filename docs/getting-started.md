# Getting Started with UnifyBuild

UnifyBuild is a .NET build orchestration system built on [NUKE](https://nuke.build/) that provides composable component interfaces for building, packing, and publishing .NET, native (CMake), and Unity projects — all driven by a single `build.config.json` file.

## Installation

### Install the CLI Tool

UnifyBuild ships as a `dotnet tool`. Add it to your repository as a local tool:

```bash
dotnet new tool-manifest   # if you don't have a .config/dotnet-tools.json yet
dotnet tool install UnifyBuild.Tool
```

Restore tools after cloning:

```bash
dotnet tool restore
```

When you are developing UnifyBuild itself on an unpublished version line, restore from the repo-local flat feed first:

```bash
task tool:bootstrap-local
dotnet tool restore
```

The repository `NuGet.config` includes `build/_artifacts/local/flat` as a package source so local tool restores work once the packages have been packed.

### Install the Library (optional)

If you're writing a custom NUKE build script, reference the library directly:

```bash
dotnet add package UnifyBuild.Nuke
```

### Unity Package Surface

Unity editor entrypoints are packaged separately under `unity/com.unifybuild.editor`.

- Use `UnifyBuild.Tool` or `UnifyBuild.Nuke` for orchestration outside the Unity editor.
- Use `com.unifybuild.editor` inside the Unity project for `-executeMethod` entrypoints and batch-mode build hooks.

This keeps NuGet consumers focused on the build foundation while the Unity package stays UPM/OpenUPM-friendly.

The repository aligns public artifact versions on a shared release line. When cutting a release, the Unity package manifest version should match the repo tag and the NuGet package version.

## Quick Start

### 1. Initialize a Configuration

Run the init command from your repository root:

```bash
dotnet unify-build init
```

This scans for `.csproj` files, groups them by directory, and generates a `build.config.json` with a `$schema` reference for IDE autocomplete.

You can also use a template:

```bash
# For library projects (NuGet packages)
dotnet unify-build init --template library

# For application projects (executables)
dotnet unify-build init --template application
```

Use `--force` to overwrite an existing config.

### 2. Review the Generated Config

A minimal `build.config.json` looks like this:

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

The `$schema` property enables autocomplete and validation in VS Code, Visual Studio, Rider, and any editor with JSON Schema support.

### 3. Build Your Projects

```bash
# Compile all projects
dotnet unify-build Compile

# Pack NuGet packages
dotnet unify-build PackProjects --configuration Release

# Publish executables
dotnet unify-build PublishHosts --configuration Release
```

## Common Commands

| Command | Description |
|---------|-------------|
| `dotnet unify-build init` | Scaffold a new `build.config.json` |
| `dotnet unify-build Compile` | Compile all configured projects |
| `dotnet unify-build PackProjects` | Pack NuGet packages for groups with `"action": "pack"` |
| `dotnet unify-build PublishHosts` | Publish executables for groups with `"action": "publish"` |
| `dotnet unify-build validate` | Validate your config against the JSON Schema |
| `dotnet unify-build doctor` | Diagnose common configuration and environment issues |

## Configuration Overview

UnifyBuild uses **project groups** to organize your builds. Each group specifies:

- **sourceDir** — where to find `.csproj` files
- **action** — what to do: `"compile"`, `"pack"`, or `"publish"`
- **include/exclude** — optional filters by project name

```json
{
  "$schema": "./build.config.schema.json",
  "artifactsVersion": "1.0.0",
  "projectGroups": {
    "libraries": {
      "sourceDir": "src/libs",
      "action": "pack",
      "include": ["MyLib.Core", "MyLib.Abstractions"]
    },
    "apps": {
      "sourceDir": "src/apps",
      "action": "publish"
    }
  }
}
```

For the full list of properties, see the [Configuration Reference](./configuration-reference.md).

## Native and Unity Builds

UnifyBuild also supports CMake-based native builds and Unity package builds. Add the relevant sections to your config:

```json
{
  "$schema": "./build.config.schema.json",
  "projectGroups": { ... },
  "nativeBuild": {
    "enabled": true,
    "cmakeSourceDir": "native",
    "buildConfig": "Release"
  },
  "unityBuild": {
    "unityProjectRoot": "unity/MyGame",
    "packages": [
      {
        "packageName": "com.example.mypackage",
        "scopedIndex": "scoped-1234",
        "sourceProjectGlobs": ["src/contracts/*"]
      }
    ]
  }
}
```

Unity export flows are intentionally split across two artifacts:

- `dotnet unify-build` owns the external orchestration targets such as `BuildForUnity` and `UnityExport`.
- `com.unifybuild.editor` supplies the Unity editor methods those targets call in batch mode.

See the [examples](./examples/) directory for complete walkthroughs.

Internal dogfooding projects and engine-specific fixtures live under `fixtures/`. They are useful for repository validation, but they are not treated as the public example surface.

## Next Steps

- [Configuration Reference](./configuration-reference.md) — all `build.config.json` properties
- [Troubleshooting](./troubleshooting.md) — common errors and how to fix them
- [Examples](./examples/) — end-to-end project examples
