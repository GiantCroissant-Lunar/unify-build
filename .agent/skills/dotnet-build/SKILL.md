---
name: dotnet-build
description: Build the UnifyBuild .NET project using the UnifyBuild.Tool dotnet tool and NUKE build system. Use when building, compiling, packing NuGet packages, or running any build-related tasks for this project.
---

# UnifyBuild .NET Build Skill

This skill provides instructions for building the UnifyBuild project using its own build tooling.

## Project Overview

UnifyBuild is a reusable build tooling system based on NUKE that ships as a dotnet tool (`UnifyBuild.Tool`). The project builds itself using its own tooling (dogfooding).

## Build System Architecture

- **Build Tool**: `UnifyBuild.Tool` (dotnet local tool)
- **Build Engine**: NUKE build system
- **Configuration**: `build/build.config.json`
- **Solution**: `dotnet/UnifyBuild.sln`
- **Projects**:
  - `UnifyBuild.Nuke` - Library for NUKE build scripts
  - `UnifyBuild.Tool` - Dotnet tool that runs build targets

## Prerequisites

Before building, ensure dotnet tools are restored:

```bash
dotnet tool restore
```

This installs the local `UnifyBuild.Tool` defined in `.config/dotnet-tools.json`.

## Common Build Commands

### Compile (Build)

Build all projects without creating packages:

```bash
dotnet tool run unify-build -- Compile --configuration Release
```

Or using the shorter alias:

```bash
dotnet unify-build Compile --configuration Release
```

### Pack Projects (Create NuGet Packages)

Build and create NuGet packages for distribution:

```bash
dotnet unify-build PackProjects --configuration Release
```

This is the most common build command. It:
1. Compiles all projects
2. Creates NuGet packages (.nupkg files)
3. Outputs to `build/_artifacts/{version}/flat/`

### Configuration Options

- `--configuration Release` - Release build (optimized, default)
- `--configuration Debug` - Debug build (with symbols)

## Build Configuration

The build is controlled by `build/build.config.json`:

```json
{
  "$schema": "./build.config.schema.json",
  "versionEnv": "GITVERSION_MAJORMINORPATCH",
  "artifactsVersion": "0.1.10",
  "projectGroups": {
    "packages": {
      "sourceDir": "dotnet/src",
      "action": "pack",
      "include": ["UnifyBuild.Nuke", "UnifyBuild.Tool"]
    }
  },
  "packIncludeSymbols": true,
  "syncLocalNugetFeed": true,
  "localNugetFeedRoot": "build/_artifacts"
}
```

Key settings:
- **artifactsVersion**: Version number for built packages
- **projectGroups.packages**: Projects to build and pack
- **sourceDir**: Location of .csproj files (`dotnet/src`)
- **action**: "pack" creates NuGet packages

## Available NUKE Targets

The UnifyBuild tool supports these targets:

- `Compile` - Build all projects
- `PackProjects` - Build and create NuGet packages
- `PublishHosts` - Publish executable/host projects
- `PublishPlugins` - Publish plugin/runtime library projects
- `PublishProjects` - Publish explicit projects from config
- `SyncLatestArtifacts` - Sync artifacts to latest folder

## Output Locations

Build artifacts are placed in:

```
build/_artifacts/
├── {version}/           # Version-specific artifacts (e.g., 0.1.10)
│   └── flat/           # NuGet packages (.nupkg, .snupkg)
└── latest/             # Symlinked to latest version (after SyncLatestArtifacts)
```

## Task Runner Integration

The project includes a `Taskfile.yml` with convenient task shortcuts:

```bash
# Using task runner (if installed)
task nuke:compile
task nuke:pack-projects
task dogfood
```

However, the direct dotnet commands are preferred as they don't require task runner installation.

## Dogfooding Workflow

The project builds itself using its own tooling:

```bash
# Build the tool packages
dotnet unify-build PackProjects --configuration Release

# Verify the build output
# (Manual verification or run build/verify-dogfood.ps1 on Windows)
```

## Troubleshooting

### Tool not found

If `dotnet unify-build` fails with "tool not found":

```bash
dotnet tool restore
```

### Build fails with missing dependencies

Ensure all NuGet packages are restored:

```bash
dotnet restore dotnet/UnifyBuild.sln
```

### Clean build

To perform a clean build, remove build artifacts first:

```bash
# Windows
Remove-Item -Recurse -Force build/_artifacts

# Unix
rm -rf build/_artifacts
```

Then run the build command again.

## Quick Reference

Most common workflow for building this project:

```bash
# 1. Restore tools
dotnet tool restore

# 2. Build and pack
dotnet unify-build PackProjects --configuration Release

# 3. Check output
ls build/_artifacts/0.1.10/flat/
```

Expected output:
- `UnifyBuild.Nuke.{version}.nupkg`
- `UnifyBuild.Tool.{version}.nupkg`
- Symbol packages (.snupkg) if enabled
