# Example: Multi-Target Framework Library

Build and pack a .NET library that targets several frameworks at once — `net8.0`, `net6.0`, and
`netstandard2.1` — from a single project.

Runnable source: [`examples/multi-target/`](https://github.com/GiantCroissant-Lunar/unify-build/tree/main/examples/multi-target).

## Project Structure

```
multi-target/
├── build.config.json
├── MultiTarget.sln
└── src/
    └── MultiTarget.Lib/
        ├── MultiTarget.Lib.csproj   # TargetFrameworks: net8.0;net6.0;netstandard2.1
        └── PlatformInfo.cs          # Uses conditional compilation
```

## Configuration

```json
{
  "$schema": "../../build/build.config.schema.json",
  "solution": "MultiTarget.sln",
  "projectGroups": {
    "packages": {
      "sourceDir": "src",
      "action": "pack",
      "include": ["MultiTarget.Lib"]
    }
  },
  "packIncludeSymbols": true
}
```

There is nothing multi-target-specific in `build.config.json` — multi-targeting is a property of
the `.csproj`, and UnifyBuild packs whatever the project produces:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net6.0;netstandard2.1</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

Note `TargetFrameworks` (plural). Framework-specific code paths use conditional compilation:

```csharp
#if NET8_0_OR_GREATER
    // modern .NET path
#endif
```

## Commands

### Compile for all target frameworks

```bash
dotnet unify-build Compile
```

### Pack the multi-target package

```bash
dotnet unify-build PackProjects --configuration Release
```

The resulting `.nupkg` contains one folder per framework:

```
lib/net8.0/
lib/net6.0/
lib/netstandard2.1/
```

## When to Use This Pattern

- Libraries consumed by projects pinned to different .NET versions
- Packages that need Unity compatibility (`netstandard2.1`) alongside modern .NET
- Gradual migration off an older framework while still supporting existing consumers

## Relationship to `unityBuild`

`netstandard2.1` in `TargetFrameworks` makes the package *consumable* by Unity via NuGet. That is
different from [`unityBuild`](../reference/configuration-reference.md#unity-build-configuration),
which builds `netstandard2.1` DLLs and copies them **directly into UPM package folders** — no
NuGet involved.

Use multi-targeting when you ship to NuGet and Unity happens to be one consumer; use `unityBuild`
when you ship a Unity package. See the [Unity Build example](unity-build.md) for the latter.

## Tips

- `packIncludeSymbols: true` produces a `.snupkg` alongside the package, covering every target
  framework.
- Keep the framework list as short as your consumers allow — each additional framework is another
  full compile on every build.
- If only some frameworks need extra MSBuild properties, set them conditionally in the `.csproj`
  rather than in `packProperties`, which applies to the whole pack operation.
