# .NET Library Example

Demonstrates building and packing a .NET class library as NuGet packages using UnifyBuild.

## Structure

```
dotnet-library/
├── build.config.json          # UnifyBuild configuration
├── DotnetLibrary.sln          # Solution file
└── src/
    ├── MyLib.Abstractions/    # Interface definitions
    │   └── IStringProcessor.cs
    └── MyLib.Core/            # Implementation library
        └── StringUtils.cs
```

## Configuration Highlights

- Uses `action: "pack"` to produce NuGet packages from both library projects
- Enables symbol packages via `packIncludeSymbols: true`
- Projects are discovered from the `src/` directory and filtered by `include`

## Commands

```bash
# Scaffold a library config
dotnet unify-build init --template library

# Compile all projects
dotnet unify-build Compile

# Pack NuGet packages
dotnet unify-build PackProjects --configuration Release
```

Packages are output to `build/_artifacts/{version}/nuget/`.

## Learn More

See the [.NET Library Example documentation](../../docs/examples/dotnet-library.md) for a detailed walkthrough.
