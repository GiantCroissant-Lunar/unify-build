# Unity Build Example

Demonstrates building .NET libraries targeting `netstandard2.1` and copying DLLs into Unity packages using UnifyBuild.

## Structure

```
unity-project/
├── build.config.json              # UnifyBuild configuration
├── UnityExample.sln               # Solution file
├── src/
│   └── Contracts/                 # .NET library (netstandard2.1)
│       ├── Contracts.csproj
│       └── PlayerData.cs
└── unity/
    └── MyGame/                    # Unity project root
        ├── Assets/
        ├── ProjectSettings/
        └── Packages/
            └── com.example.contracts/
                ├── package.json   # Unity package manifest
                └── Runtime/       # DLLs are copied here by UnifyBuild
```

## Configuration Highlights

- `unityBuild.targetFramework` is set to `netstandard2.1` (required for Unity compatibility)
- `unityBuild.unityProjectRoot` points to the Unity project containing `Assets/`
- Package mappings link .NET projects to Unity package directories
- Built DLLs are automatically copied to each package's `Runtime/` folder

## Commands

```bash
# Compile and copy DLLs into Unity packages
dotnet unify-build Compile

# Validate Unity build configuration
dotnet unify-build Validate
```

## Prerequisites

- .NET source projects must target `netstandard2.1` (Unity does not support `net6.0`+)
- Unity project must have `Assets/` and `ProjectSettings/` directories

## Learn More

See the [Unity Build Example documentation](../../docs/examples/unity-build.md) for package mapping examples, dependency DLL handling, and troubleshooting.
