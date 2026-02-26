# Configuration Reference

This document covers every property available in `build.config.json`. Add a `$schema` reference at the top of your file for IDE autocomplete and validation:

```json
{
  "$schema": "./build.config.schema.json"
}
```

## Top-Level Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `$schema` | `string` | — | Path to the JSON Schema file for IDE support |
| `version` | `string?` | `null` | Explicit build version. If omitted, resolved from environment variables |
| `versionEnv` | `string?` | `"Version"` | Environment variable name to read version from |
| `artifactsVersion` | `string?` | `null` | Version string used for the `build/_artifacts/{version}` layout |
| `solution` | `string?` | `null` | Path to `.sln` file relative to repo root |
| `projectGroups` | `object` | — | **Required.** Map of group name → project group configuration |
| `compileProjects` | `string[]?` | `null` | Explicit project paths to compile (fallback) |
| `publishProjects` | `string[]?` | `null` | Explicit project paths to publish (fallback) |
| `packProjects` | `string[]?` | `null` | Explicit project paths to pack (fallback) |
| `nuGetOutputDir` | `string?` | `null` | Custom NuGet output directory. Default: `build/_artifacts/{version}/nuget` |
| `publishOutputDir` | `string?` | `null` | Custom publish output directory. Default: `build/_artifacts/{version}` |
| `packProperties` | `object?` | `null` | Additional MSBuild properties for pack operations |
| `packIncludeSymbols` | `bool` | `false` | Include symbol packages when packing |
| `nativeBuild` | `object?` | `null` | CMake native build configuration |
| `unityBuild` | `object?` | `null` | Unity package build configuration |

### Local NuGet Feed Sync

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `syncLocalNugetFeed` | `bool` | `false` | Enable syncing packages to a local NuGet feed |
| `localNugetFeedRoot` | `string?` | `null` | Root directory of the local feed |
| `localNugetFeedFlatSubdir` | `string?` | `"flat"` | Subdirectory for flat feed layout |
| `localNugetFeedHierarchicalSubdir` | `string?` | `"hierarchical"` | Subdirectory for hierarchical feed layout |
| `localNugetFeedBaseUrl` | `string?` | `null` | Base URL for the local feed |

## Version Resolution

UnifyBuild resolves the build version in this order:

1. `version` property in config (explicit)
2. Environment variable named by `versionEnv` (default: `Version`)
3. External version passed programmatically (e.g., from GitVersion)
4. `GITVERSION_MAJORMINORPATCH` environment variable
5. `artifactsVersion` property
6. Fallback: `"0.1.0"`

## Project Groups

Project groups are the core organizational unit. Each group maps a source directory to a build action.

```json
{
  "projectGroups": {
    "my-group-name": {
      "sourceDir": "src/libs",
      "action": "pack",
      "include": ["ProjectA", "ProjectB"],
      "exclude": ["ProjectC"],
      "outputDir": "custom/output",
      "properties": {
        "CustomProp": "value"
      }
    }
  }
}
```

### Project Group Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `sourceDir` | `string` | `""` | Directory containing projects, relative to repo root |
| `action` | `string` | `"compile"` | Build action: `"compile"`, `"pack"`, or `"publish"` |
| `include` | `string[]?` | `null` | Project names to include (without `.csproj`). If omitted, all projects in `sourceDir` are included |
| `exclude` | `string[]?` | `null` | Project names to exclude (without `.csproj`) |
| `outputDir` | `string?` | `null` | Override output directory for this group |
| `properties` | `object?` | `null` | Additional MSBuild properties for this group |

### Actions

| Action | Description | Target |
|--------|-------------|--------|
| `compile` | Build projects without producing deployable output | `Compile` |
| `pack` | Create NuGet packages | `PackProjects` |
| `publish` | Publish self-contained executables | `PublishHosts` |

### Project Discovery

Within each group, UnifyBuild recursively searches `sourceDir` for `.csproj` files, automatically excluding `bin/`, `obj/`, `.git/`, and `node_modules/` directories. Use `include` and `exclude` to filter by project name (without the `.csproj` extension).

## Native Build Configuration

Configure CMake-based C++ builds alongside your .NET projects.

```json
{
  "nativeBuild": {
    "enabled": true,
    "cmakeSourceDir": "native",
    "cmakeBuildDir": "native/build",
    "cmakePreset": "default",
    "cmakeOptions": ["-DBUILD_SHARED_LIBS=ON"],
    "buildConfig": "Release",
    "autoDetectVcpkg": true,
    "outputDir": "build/_artifacts/1.0.0/native",
    "artifactPatterns": ["*.dll", "*.so", "*.dylib"]
  }
}
```

### Native Build Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `enabled` | `bool` | `true` | Enable/disable native builds |
| `cmakeSourceDir` | `string?` | `"native"` | Directory containing `CMakeLists.txt` |
| `cmakeBuildDir` | `string?` | `"native/build"` | CMake build output directory |
| `cmakePreset` | `string?` | `null` | CMake preset name (requires `CMakePresets.json`) |
| `cmakeOptions` | `string[]?` | `null` | Additional CMake configuration flags |
| `buildConfig` | `string?` | `"Release"` | Build configuration (`Release`, `Debug`, etc.) |
| `autoDetectVcpkg` | `bool` | `true` | Auto-detect and use vcpkg toolchain if present |
| `outputDir` | `string?` | `null` | Output directory for native artifacts. Default: `build/_artifacts/{version}/native` |
| `artifactPatterns` | `string[]?` | `["*.dll", "*.so", "*.dylib", "*.lib", "*.a"]` | File patterns to collect as build artifacts |

If `nativeBuild` is omitted but a `native/CMakeLists.txt` exists, UnifyBuild auto-detects and configures native builds with defaults.

## Unity Build Configuration

Build .NET libraries targeting `netstandard2.1` and copy DLLs into Unity packages.

```json
{
  "unityBuild": {
    "targetFramework": "netstandard2.1",
    "unityProjectRoot": "unity/MyGame",
    "packages": [
      {
        "packageName": "com.example.contracts",
        "scopedIndex": "scoped-1234",
        "sourceProjects": ["src/Contracts/Contracts.csproj"],
        "sourceProjectGlobs": ["project/contracts/*"],
        "dependencyDlls": ["Newtonsoft.Json"]
      }
    ]
  }
}
```

### Unity Build Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `targetFramework` | `string?` | `"netstandard2.1"` | Target framework for Unity-compatible builds |
| `unityProjectRoot` | `string` | `""` | Root directory of the Unity project, relative to repo root |
| `packages` | `array?` | `null` | Array of Unity package mapping configurations |

### Unity Package Mapping Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `packageName` | `string` | `""` | Unity package name (e.g., `"com.example.mypackage"`) |
| `scopedIndex` | `string` | `""` | Scoped registry index directory |
| `sourceProjects` | `string[]?` | `null` | Explicit `.csproj` paths to build |
| `sourceProjectGlobs` | `string[]?` | `null` | Glob patterns for discovering source projects |
| `dependencyDlls` | `string[]?` | `null` | Transitive dependency DLL names to copy from build output |

## Complete Example

```json
{
  "$schema": "./build.config.schema.json",
  "version": null,
  "versionEnv": "Version",
  "artifactsVersion": "local",
  "solution": "src/MySolution.sln",
  "projectGroups": {
    "packages": {
      "sourceDir": "src",
      "action": "pack",
      "include": ["MyLib.Core", "MyLib.Abstractions"]
    },
    "apps": {
      "sourceDir": "src/apps",
      "action": "publish"
    },
    "tools": {
      "sourceDir": "src/tools",
      "action": "compile",
      "exclude": ["MyTool.Benchmarks"]
    }
  },
  "packIncludeSymbols": true,
  "packProperties": {
    "UseDevelopmentReferences": "false"
  },
  "nativeBuild": {
    "enabled": true,
    "cmakeSourceDir": "native",
    "buildConfig": "Release",
    "autoDetectVcpkg": true
  }
}
```
