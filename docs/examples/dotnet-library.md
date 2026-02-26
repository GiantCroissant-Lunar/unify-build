# Example: .NET Library Project

Build and pack a .NET class library as a NuGet package using UnifyBuild.

## Project Structure

```
my-library/
├── build.config.json
├── build.config.schema.json
├── src/
│   ├── MyLib.Core/
│   │   ├── MyLib.Core.csproj
│   │   └── StringUtils.cs
│   └── MyLib.Abstractions/
│       ├── MyLib.Abstractions.csproj
│       └── IStringProcessor.cs
├── tests/
│   └── MyLib.Core.Tests/
│       ├── MyLib.Core.Tests.csproj
│       └── StringUtilsTests.cs
└── MyLibrary.sln
```

## Configuration

```json
{
  "$schema": "./build.config.schema.json",
  "solution": "MyLibrary.sln",
  "projectGroups": {
    "packages": {
      "sourceDir": "src",
      "action": "pack",
      "include": ["MyLib.Core", "MyLib.Abstractions"]
    }
  },
  "packIncludeSymbols": true,
  "packProperties": {
    "UseDevelopmentReferences": "false"
  }
}
```

This config tells UnifyBuild to:

- Discover `.csproj` files under `src/`
- Include only `MyLib.Core` and `MyLib.Abstractions` (the test project is excluded)
- Pack them as NuGet packages with symbol packages

## Commands

### Scaffold the config

```bash
dotnet unify-build init --template library
```

### Compile

```bash
dotnet unify-build Compile
```

### Pack NuGet packages

```bash
dotnet unify-build PackProjects --configuration Release
```

### Expected output

```
═══════════════════════════════════════
Target: Compile
═══════════════════════════════════════
  Building MyLib.Core...
  Building MyLib.Abstractions...
  ✓ Compile completed

═══════════════════════════════════════
Target: PackProjects
═══════════════════════════════════════
  Packing MyLib.Core → build/_artifacts/1.0.0/nuget/MyLib.Core.1.0.0.nupkg
  Packing MyLib.Abstractions → build/_artifacts/1.0.0/nuget/MyLib.Abstractions.1.0.0.nupkg
  ✓ PackProjects completed
```

Packages are written to `build/_artifacts/{version}/nuget/`.

## Tips

- Use `include` to limit which projects get packed — test projects and benchmarks should be excluded.
- Set `packIncludeSymbols: true` for better debugging experience for consumers.
- Version is resolved automatically from GitVersion or the `version` / `artifactsVersion` properties. See [Configuration Reference](../configuration-reference.md#version-resolution) for the full resolution order.
