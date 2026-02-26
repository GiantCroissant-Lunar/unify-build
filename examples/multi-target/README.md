# Multi-Target Framework Example

Demonstrates building a .NET library that targets multiple frameworks (`net8.0`, `net6.0`, `netstandard2.1`) using UnifyBuild.

## Structure

```
multi-target/
├── build.config.json          # UnifyBuild configuration
├── MultiTarget.sln            # Solution file
└── src/
    └── MultiTarget.Lib/
        ├── MultiTarget.Lib.csproj   # Multi-target: net8.0;net6.0;netstandard2.1
        └── PlatformInfo.cs          # Uses conditional compilation
```

## Configuration Highlights

- The `.csproj` uses `<TargetFrameworks>` (plural) to target multiple frameworks
- UnifyBuild packs all target frameworks into a single NuGet package automatically
- Conditional compilation (`#if NET8_0_OR_GREATER`) enables framework-specific code paths

## Commands

```bash
# Compile for all target frameworks
dotnet unify-build Compile

# Pack multi-target NuGet package
dotnet unify-build PackProjects --configuration Release
```

The resulting `.nupkg` contains `lib/net8.0/`, `lib/net6.0/`, and `lib/netstandard2.1/` folders.

## When to Use This Pattern

- Libraries consumed by projects on different .NET versions
- Packages that need Unity compatibility (`netstandard2.1`) alongside modern .NET
- Gradual migration from older frameworks while supporting existing consumers
