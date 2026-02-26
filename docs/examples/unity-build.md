# Example: Unity Build Integration

Build .NET libraries targeting `netstandard2.1` and copy the output DLLs into Unity packages using UnifyBuild.

## Overview

UnifyBuild's Unity integration bridges .NET library development with Unity's package system. You write your shared code as standard .NET projects, and UnifyBuild compiles them to `netstandard2.1` and copies the resulting DLLs into your Unity package directories automatically.

This workflow is ideal for:

- Sharing contracts, data models, or networking code between a .NET server and a Unity client
- Maintaining strongly-typed APIs across both runtimes
- Keeping your Unity project free of raw C# source files that need recompilation inside the Unity editor

## Project Structure Requirements

A typical Unity + .NET repository follows this layout:

```
my-unity-project/
├── build.config.json              # UnifyBuild configuration
├── build.config.schema.json       # JSON Schema for IDE autocomplete
├── src/                           # .NET source projects
│   ├── Contracts/
│   │   ├── Contracts.csproj       # MUST target netstandard2.1
│   │   └── PlayerData.cs
│   └── Networking/
│       ├── Networking.csproj      # MUST target netstandard2.1
│       └── NetworkClient.cs
├── unity/
│   └── MyGame/                    # Unity project root
│       ├── Assets/                # Required — Unity expects this
│       ├── Packages/
│       │   └── com.example.contracts/
│       │       ├── package.json   # Unity package manifest
│       │       └── Runtime/       # DLLs are copied here
│       └── ProjectSettings/       # Required — Unity expects this
└── MyUnityProject.sln
```

### Directory Requirements

- **Unity project root** must contain an `Assets/` directory and a `ProjectSettings/` directory. UnifyBuild validates this path exists.
- **Source projects** must be standard .NET class library projects (`.csproj` files).
- **Package directories** follow Unity's local package convention under `Packages/`. Each package needs a `package.json` and a `Runtime/` folder where DLLs land.

### Naming Conventions

- Unity package names use reverse-domain notation: `com.company.packagename`
- Scoped indices (e.g., `scoped-3208`) correspond to your scoped registry configuration in Unity's `manifest.json`
- Source project names should match the assembly name you want in Unity

## Target Framework Guidance

### Why `netstandard2.1`?

Unity's scripting runtime is based on Mono/.NET and supports `netstandard2.1` as the highest compatible target. This means:

| Framework | Unity Compatible? | Notes |
|-----------|:-:|-------|
| `netstandard2.0` | ✅ | Works, but fewer APIs available |
| `netstandard2.1` | ✅ | **Recommended** — best API coverage with Unity support |
| `net6.0` / `net7.0` / `net8.0` | ❌ | Not supported by Unity's runtime |
| `net48` (Framework) | ❌ | Not recommended for cross-platform Unity |

If you omit `targetFramework` from the Unity build config, UnifyBuild defaults to `netstandard2.1`.

### Setting the Target Framework

In your `.csproj` files:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
  </PropertyGroup>
</Project>
```

UnifyBuild validates that the configured target framework is compatible. If you set an unsupported framework like `net8.0`, validation will produce a warning.

## Configuration

### Full `build.config.json` Example

```json
{
  "$schema": "./build.config.schema.json",
  "solution": "MyUnityProject.sln",
  "projectGroups": {
    "unity-libs": {
      "sourceDir": "src",
      "action": "compile",
      "include": ["Contracts", "Networking"]
    }
  },
  "unityBuild": {
    "targetFramework": "netstandard2.1",
    "unityProjectRoot": "unity/MyGame",
    "packages": [
      {
        "packageName": "com.example.contracts",
        "scopedIndex": "scoped-1234",
        "sourceProjects": ["src/Contracts/Contracts.csproj"],
        "dependencyDlls": ["Newtonsoft.Json.dll"]
      },
      {
        "packageName": "com.example.networking",
        "scopedIndex": "scoped-5678",
        "sourceProjects": ["src/Networking/Networking.csproj"],
        "sourceProjectGlobs": [],
        "dependencyDlls": []
      }
    ]
  }
}
```

### Configuration Properties

| Property | Type | Required | Description |
|----------|------|:--------:|-------------|
| `targetFramework` | string | No | .NET target framework. Default: `"netstandard2.1"` |
| `unityProjectRoot` | string | **Yes** | Path to the Unity project (the folder containing `Assets/`) |
| `packages` | array | **Yes** | Array of package mappings linking .NET projects to Unity packages |

### Package Mapping Properties

| Property | Type | Required | Description |
|----------|------|:--------:|-------------|
| `packageName` | string | **Yes** | Unity package identifier (e.g., `"com.company.package"`) |
| `scopedIndex` | string | **Yes** | Scoped registry index for the package |
| `sourceProjects` | string[] | No | Explicit `.csproj` paths to build and copy |
| `sourceProjectGlobs` | string[] | No | Glob patterns for project discovery |
| `dependencyDlls` | string[] | No | Transitive dependency DLL names to include |

At least one of `sourceProjects` or `sourceProjectGlobs` should be provided per package mapping.

## Package Mapping Examples

### Single Project with Explicit Path

The simplest mapping — one .NET project maps to one Unity package:

```json
{
  "unityBuild": {
    "unityProjectRoot": "unity/MyGame",
    "packages": [
      {
        "packageName": "com.example.contracts",
        "scopedIndex": "scoped-1234",
        "sourceProjects": ["src/Contracts/Contracts.csproj"],
        "dependencyDlls": []
      }
    ]
  }
}
```

### Glob-Based Project Discovery

Use `sourceProjectGlobs` to discover projects by pattern instead of listing each one explicitly. This is useful when you have many projects under a common directory:

```json
{
  "unityBuild": {
    "unityProjectRoot": "unity/MyGame",
    "packages": [
      {
        "packageName": "com.example.contracts",
        "scopedIndex": "scoped-1234",
        "sourceProjectGlobs": ["src/Contracts/*"],
        "dependencyDlls": ["Newtonsoft.Json.dll"]
      }
    ]
  }
}
```

Each glob pattern is expanded to find `.csproj` files in matching directories. For example, `src/Contracts/*` discovers all `.csproj` files in immediate subdirectories of `src/Contracts/`.

### Multiple Packages with Dependencies

A real-world setup often has multiple packages, some with transitive dependencies:

```json
{
  "unityBuild": {
    "unityProjectRoot": "unity/MyGame",
    "targetFramework": "netstandard2.1",
    "packages": [
      {
        "packageName": "com.example.contracts",
        "scopedIndex": "scoped-1234",
        "sourceProjects": ["src/Contracts/Contracts.csproj"],
        "dependencyDlls": ["Newtonsoft.Json.dll"]
      },
      {
        "packageName": "com.example.networking",
        "scopedIndex": "scoped-5678",
        "sourceProjects": ["src/Networking/Networking.csproj"],
        "dependencyDlls": ["System.Buffers.dll", "System.Memory.dll"]
      },
      {
        "packageName": "com.example.analytics",
        "scopedIndex": "scoped-9012",
        "sourceProjectGlobs": ["src/Analytics/*"],
        "dependencyDlls": []
      }
    ]
  }
}
```

Notes:
- Each package mapping is independent — a source project can appear in multiple packages if needed.
- The `dependencyDlls` list should include the full DLL filename (e.g., `"Newtonsoft.Json.dll"`). UnifyBuild resolves the full path from the build output directory.
- Dependency DLLs are searched across all source project build outputs, so a dependency built by one project can be copied into another package.

## Commands

### Compile and Copy DLLs into Unity Packages

```bash
dotnet unify-build Compile
```

The Unity build step runs after compilation, copying the built DLLs into each package's `Runtime/` directory.

### Expected Output

```
═══════════════════════════════════════
Target: Compile
═══════════════════════════════════════
  Building Contracts (netstandard2.1)...
  Building Networking (netstandard2.1)...
  ✓ Compile completed

═══════════════════════════════════════
Target: BuildForUnity
═══════════════════════════════════════
  Package: com.example.contracts
    Copying Contracts.dll → unity/MyGame/Packages/com.example.contracts/Runtime/
    Copying Newtonsoft.Json.dll → unity/MyGame/Packages/com.example.contracts/Runtime/
  Package: com.example.networking
    Copying Networking.dll → unity/MyGame/Packages/com.example.networking/Runtime/
  ✓ BuildForUnity completed
```

### Validate Your Configuration

Run the validate command to check your Unity build config before building:

```bash
dotnet unify-build Validate
```

This checks that:
- The Unity project path exists and contains `Assets/`
- The target framework is Unity-compatible (`netstandard2.0` or `netstandard2.1`)
- All source project paths in package mappings are valid

## Troubleshooting

### Unity project not found (UB104)

**Symptom:** Validation error "Unity project root does not exist"

**Fix:** Verify `unityProjectRoot` points to the directory containing `Assets/` and `ProjectSettings/`. The path is relative to the repository root.

```json
{
  "unityBuild": {
    "unityProjectRoot": "unity/MyGame"
  }
}
```

Check that `unity/MyGame/Assets/` exists on disk.

### Incompatible target framework (UB102)

**Symptom:** Validation warning about target framework compatibility

**Fix:** Unity only supports `netstandard2.0` and `netstandard2.1`. If you've set a different framework, update it:

```json
{
  "unityBuild": {
    "targetFramework": "netstandard2.1"
  }
}
```

Also update your `.csproj` files to match.

### DLL not copied

**Symptom:** Build succeeds but DLLs don't appear in the Unity package `Runtime/` directory

**Causes:**
1. The source project doesn't target `netstandard2.1` — check the `.csproj` `<TargetFramework>`
2. The project failed to compile — check build output for errors above the Unity step
3. The output path doesn't match — ensure `Configuration` (Debug/Release) matches your build

### Missing dependency DLL (UB203)

**Symptom:** Warning "Dependency DLL not found in any build output"

**Fix:** Add the dependency name to `dependencyDlls`. The DLL must be present in the build output directory after compilation. Common causes:
- The dependency is a development-only package (not copied to output)
- The DLL name is misspelled — check the exact filename in `bin/Release/netstandard2.1/`
- The dependency needs `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` in the `.csproj`

### Source project not found

**Symptom:** Validation error for a `sourceProjects` path

**Fix:** Paths in `sourceProjects` are relative to the repository root. Verify the path:

```bash
# Check the file exists
ls src/Contracts/Contracts.csproj
```

### Glob pattern matches nothing

**Symptom:** A package mapping with `sourceProjectGlobs` produces no DLLs

**Fix:** Glob patterns expand to find `.csproj` files in matching directories. Verify your pattern:
- `src/Contracts/*` matches `.csproj` files in immediate subdirectories of `src/Contracts/`
- Ensure the directories exist and contain `.csproj` files

See [Troubleshooting](../troubleshooting.md#ub203--unity-build-failed) for more error codes and solutions.
