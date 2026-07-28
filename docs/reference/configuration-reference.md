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
| `nativeBuild` | `object?` | `null` | [CMake native build](#native-build-configuration) configuration |
| `rustBuild` | `object?` | `null` | [Rust/Cargo build](#rust-build-configuration) configuration |
| `goBuild` | `object?` | `null` | [Go build](#go-build-configuration) configuration |
| `unityBuild` | `object?` | `null` | [Unity package build](#unity-build-configuration) configuration (DLL copying) |
| `unityExport` | `object?` | `null` | [Unity platform export](#unity-export-configuration) configuration (player builds) |
| `godotBuild` | `object?` | `null` | [Godot export](#godot-build-configuration) configuration |
| `mobileBuild` | `object?` | `null` | [Mobile/Fastlane](#mobile-build-configuration) configuration |
| `performance` | `object?` | `null` | [Caching and change detection](#performance-configuration) |
| `observability` | `object?` | `null` | [Metrics and telemetry](#observability-configuration) |
| `packageManagement` | `object?` | `null` | [Registries, signing, SBOM, retention](#package-management-configuration) |
| `extensions` | `object?` | `null` | [Custom component plugin loading](#extensions-configuration) |

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

## Rust Build Configuration

Build Rust crates via Cargo alongside your .NET projects.

!!! note "Requires a custom NUKE build"
    `IUnifyRust` is **not** composed into the `unify-build` CLI tool. To use the `RustBuild`
    target, implement `IUnifyRust` in your own NUKE build class. See [Targets](targets.md#availability).

```json
{
  "rustBuild": {
    "enabled": true,
    "cargoManifestDir": "native/rust",
    "profile": "release",
    "features": ["simd"],
    "targetTriple": "x86_64-pc-windows-msvc",
    "outputDir": "build/_artifacts/1.0.0/rust",
    "artifactPatterns": ["*.dll", "*.so", "*.exe"]
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `enabled` | `bool` | `true` | Enable/disable Rust builds |
| `cargoManifestDir` | `string?` | `null` | Directory containing `Cargo.toml`, relative to repo root |
| `profile` | `string` | `"release"` | Cargo build profile (`debug`, `release`, or a custom profile) |
| `features` | `string[]?` | `null` | Cargo features to enable |
| `targetTriple` | `string?` | `null` | Target triple for cross-compilation (e.g. `x86_64-pc-windows-msvc`) |
| `outputDir` | `string?` | `null` | Output directory. Default: `build/_artifacts/{version}/rust` |
| `artifactPatterns` | `string[]?` | `null` | File patterns to collect as artifacts |

## Go Build Configuration

Build Go modules as part of the same pipeline.

!!! note "Requires a custom NUKE build"
    `IUnifyGo` is **not** composed into the `unify-build` CLI tool. To use the `GoBuild`
    target, implement `IUnifyGo` in your own NUKE build class. See [Targets](targets.md#availability).

```json
{
  "goBuild": {
    "enabled": true,
    "goModuleDir": "tools/agent",
    "buildFlags": ["-ldflags", "-s -w"],
    "outputBinary": "agent.exe",
    "outputDir": "build/_artifacts/1.0.0/go",
    "envVars": { "GOOS": "windows", "GOARCH": "amd64" }
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `enabled` | `bool` | `true` | Enable/disable Go builds |
| `goModuleDir` | `string?` | `null` | Directory containing `go.mod`, relative to repo root |
| `buildFlags` | `string[]?` | `null` | Flags passed to `go build` |
| `outputBinary` | `string?` | `null` | Output binary name (passed via `-o`) |
| `outputDir` | `string?` | `null` | Output directory. Default: `build/_artifacts/{version}/go` |
| `envVars` | `object?` | `null` | Environment variables for the `go build` process (e.g. `GOOS`, `GOARCH`) |

## Unity Export Configuration

`unityExport` builds actual Unity **players** by driving the Unity Editor in batch mode. This is
distinct from [`unityBuild`](#unity-build-configuration), which only compiles `netstandard2.1`
DLLs and copies them into Unity packages. The two are independent and can be used together.

```json
{
  "unityExport": {
    "projectRoot": "unity/MyGame",
    "editorPathEnv": "UNITY_EDITOR_PATH",
    "executeMethod": "UnifyBuild.Editor.BuildScript.Build",
    "useFastlaneForMobile": true,
    "platforms": [
      { "buildTarget": "StandaloneWindows64", "outputName": "MyGame.exe" },
      { "buildTarget": "Android", "outputName": "MyGame", "buildArgs": { "UNITY_SCRIPTING_BACKEND": "IL2CPP" } }
    ]
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `projectRoot` | `string` | `""` | Root directory of the Unity project (contains `Assets/`) |
| `editorPath` | `string?` | `null` | Explicit path to the Unity Editor executable |
| `editorPathEnv` | `string?` | `"UNITY_EDITOR_PATH"` | Environment variable holding the Unity Editor path |
| `executeMethod` | `string?` | `"UnifyBuild.Editor.BuildScript.Build"` | Static method invoked via `-executeMethod` |
| `platforms` | `array?` | `null` | Platform export configurations |
| `useFastlaneForMobile` | `bool` | `false` | Hand mobile exports to Fastlane after export |
| `outputDir` | `string?` | `null` | Output root. Default: `build/_artifacts/{version}/unity-export` |

### Unity Export Platform Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `buildTarget` | `string` | `""` | Unity `BuildTarget` name (`StandaloneWindows64`, `Android`, `iOS`, `StandaloneOSX`, …) |
| `outputName` | `string?` | `null` | Output file or directory name for this platform |
| `buildArgs` | `object?` | `null` | Extra arguments passed to the Unity build method as environment variables |

The `executeMethod` default resolves to the entrypoint shipped in the `com.unifybuild.editor`
Unity package. Mobile targets (`Android`, `iOS`) export native Gradle/Xcode projects rather than
finished binaries — those are then built by the [mobile targets](#mobile-build-configuration).

## Godot Build Configuration

Export Godot projects. Desktop platforms produce final binaries; mobile platforms export native
Gradle/Xcode projects for Fastlane to build.

```json
{
  "godotBuild": {
    "projectRoot": "godot/MyGame",
    "executablePathEnv": "GODOT",
    "assemblyName": "MyGame",
    "useFastlaneForMobile": true,
    "platforms": [
      { "rid": "win-x64", "presetName": "Windows Desktop", "binaryName": "MyGame.exe" },
      { "rid": "linux-x64", "presetName": "Linux", "binaryName": "MyGame.x86_64" }
    ]
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `projectRoot` | `string` | `""` | Root directory containing `project.godot` |
| `executablePathEnv` | `string?` | `"GODOT"` | Environment variable holding the Godot executable path |
| `executablePath` | `string?` | `null` | Explicit path to the Godot executable. Overrides `executablePathEnv` |
| `assemblyName` | `string?` | `null` | Name of the central game assembly |
| `platforms` | `array?` | `null` | Platforms to export |
| `androidKeystorePath` | `string?` | `null` | Android keystore for signing APK/AAB. Also settable via `GODOT_ANDROID_KEYSTORE_PATH` |
| `useFastlaneForMobile` | `bool` | `false` | Hand mobile exports to Fastlane after export |

Artifacts are written to `build/_artifacts/{version}/godot`.

### Godot Export Platform Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `rid` | `string` | `""` | .NET Runtime Identifier (e.g. `win-x64`, `linux-x64`, `osx`) |
| `presetName` | `string` | `""` | Godot export preset name (e.g. `"Windows Desktop"`) |
| `binaryName` | `string` | `""` | Name of the exported binary (e.g. `"complete-app.zip"`) |

!!! info "`rid` does not drive publishing"
    `rid` is used for the output directory name and version metadata only. Godot's own C# export
    plugin performs the per-architecture `dotnet publish` during `--export-release`.

## Mobile Build Configuration

Build and distribute iOS/Android apps through Fastlane. Works either from a standalone mobile
project or from Gradle/Xcode projects exported by [`unityExport`](#unity-export-configuration) or
[`godotBuild`](#godot-build-configuration).

```json
{
  "mobileBuild": {
    "enabled": true,
    "mobileRoot": "mobile",
    "ios": {
      "enabled": true,
      "buildLane": "build",
      "betaLane": "beta",
      "releaseLane": "release",
      "envVars": { "FASTLANE_APPLE_ID": "dev@example.com" }
    },
    "android": {
      "enabled": true,
      "betaLane": "internal"
    }
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `enabled` | `bool` | `true` | Enable/disable mobile builds |
| `mobileRoot` | `string?` | `"mobile"` | Directory containing `ios/` and `android/` subdirectories |
| `ios` | `object?` | `null` | iOS platform configuration |
| `android` | `object?` | `null` | Android platform configuration |
| `outputDir` | `string?` | `null` | Output directory. Default: `build/_artifacts/{version}/mobile` |

### Mobile Platform Properties

Applies to both `ios` and `android`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `enabled` | `bool` | `true` | Enable this platform |
| `buildLane` | `string?` | `"build"` | Fastlane lane used by the build targets |
| `betaLane` | `string?` | `"beta"` | Fastlane lane for beta distribution (TestFlight / Play internal) |
| `releaseLane` | `string?` | `"release"` | Fastlane lane for store release |
| `envVars` | `object?` | `null` | Additional environment variables passed to Fastlane |

If `mobileBuild` is omitted, mobile support activates automatically when a `mobile/` directory
exists at the repo root.

## Performance Configuration

Controls build caching, change detection, and parallelism. See [Caching](caching.md) for the
cache key model and invalidation rules.

```json
{
  "performance": {
    "enableCache": true,
    "cacheDir": "build/_cache",
    "enableChangeDetection": true,
    "distributedCacheUrl": "https://cache.example.com/unify-build",
    "maxParallelism": 8
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `enableCache` | `bool` | `false` | Enable local build caching |
| `cacheDir` | `string?` | `null` | Cache storage directory. Default: `build/_cache` |
| `enableChangeDetection` | `bool` | `true` | Enable file-based change detection |
| `distributedCacheUrl` | `string?` | `null` | Distributed cache server. Entries are uploaded/downloaded via HTTP `PUT`/`GET` with the cache key as the URL path segment |
| `maxParallelism` | `int?` | `null` | Max parallelism for building independent project groups. Defaults to the processor count when `null` or `<= 0` |

## Observability Configuration

Controls build metrics and opt-in local telemetry. See [Telemetry](telemetry.md) for the emitted
record shape; exported JSON can be loaded into the analytics dashboard under `dashboard/`.

```json
{
  "observability": {
    "enableMetrics": true,
    "metricsFormat": "json",
    "metricsOutputDir": "build/_metrics",
    "enableTelemetry": false
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `enableMetrics` | `bool` | `true` | Enable build metrics collection |
| `metricsFormat` | `string` | `"json"` | Metrics report format: `"json"` or `"csv"` |
| `metricsOutputDir` | `string?` | `null` | Metrics output directory. Default: `build/_metrics` |
| `enableTelemetry` | `bool` | `false` | Enable anonymous telemetry (opt-in). Data is stored locally and never sent to a remote server |

## Package Management Configuration

Multi-registry publishing, package signing, SBOM generation, and local feed retention.

```json
{
  "packageManagement": {
    "registries": [
      { "name": "NuGet.org", "url": "https://api.nuget.org/v3/index.json", "apiKeyEnvVar": "NUGET_API_KEY" },
      { "name": "GitHub Packages", "url": "https://nuget.pkg.github.com/OWNER/index.json", "apiKeyEnvVar": "GH_PACKAGES_TOKEN" }
    ],
    "signing": {
      "certificatePath": "build/signing/cert.pfx",
      "certificatePasswordEnvVar": "SIGNING_CERT_PASSWORD",
      "timestampUrl": "http://timestamp.digicert.com"
    },
    "sbom": { "format": "spdx", "outputDir": "build/_artifacts/1.0.0/sbom" },
    "retention": { "maxVersions": 10, "maxAgeDays": 90, "localFeedPath": "C:/packages/nuget" }
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `registries` | `array?` | `null` | NuGet registries to publish to |
| `signing` | `object?` | `null` | Package signing configuration |
| `sbom` | `object?` | `null` | SBOM generation configuration |
| `retention` | `object?` | `null` | Retention policy for local feed cleanup |

### Registry Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `name` | `string` | `""` | Display name (e.g. `"NuGet.org"`) |
| `url` | `string` | `""` | Registry URL |
| `apiKeyEnvVar` | `string?` | `null` | Environment variable holding the API key |

### Signing Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `certificatePath` | `string?` | `null` | Path to the signing certificate (`.pfx`) |
| `certificatePasswordEnvVar` | `string?` | `null` | Environment variable holding the certificate password |
| `timestampUrl` | `string?` | `null` | Timestamp server URL for countersigning |

!!! warning "Never inline secrets"
    Both `apiKeyEnvVar` and `certificatePasswordEnvVar` take the **name of an environment
    variable**, not the secret itself. `build.config.json` is committed to source control.

### SBOM Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `format` | `string` | `"spdx"` | SBOM format: `"spdx"` or `"cyclonedx"` |
| `outputDir` | `string?` | `null` | Output directory for generated SBOM files |

### Retention Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `maxVersions` | `int?` | `null` | Maximum versions to keep per package |
| `maxAgeDays` | `int?` | `null` | Maximum package age in days; older packages are removed |
| `localFeedPath` | `string?` | `null` | Local NuGet feed directory to apply retention to |

## Extensions Configuration

Load custom component interfaces from external assemblies.

```json
{
  "extensions": {
    "pluginPaths": ["build/plugins/MyCompany.UnifyBuild.Extras.dll"],
    "autoLoadPlugins": false
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `pluginPaths` | `string[]?` | `null` | Paths to plugin assemblies (`.dll`) or directories containing them, relative to repo root |
| `autoLoadPlugins` | `bool` | `false` | Auto-discover and load plugins from `build/plugins/` |

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
