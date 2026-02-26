# .NET Application Example

Demonstrates building and publishing a .NET console application using UnifyBuild.

## Structure

```
dotnet-application/
├── build.config.json       # UnifyBuild configuration
├── DotnetApp.sln           # Solution file
└── src/
    ├── MyApp.Api/           # Executable application
    │   └── Program.cs
    └── MyApp.Shared/        # Shared library
        └── Models.cs
```

## Configuration Highlights

- Uses `action: "publish"` for the executable project to produce a deployable output
- Uses `action: "compile"` for the shared library (it's referenced by the host, no separate output needed)
- Two project groups separate hosts from libraries

## Commands

```bash
# Scaffold an application config
dotnet unify-build init --template application

# Compile everything
dotnet unify-build Compile

# Publish executables
dotnet unify-build PublishHosts --configuration Release
```

Published output lands in `build/_artifacts/{version}/{projectName}/`.

## Learn More

See the [.NET Application Example documentation](../../docs/examples/dotnet-application.md) for a detailed walkthrough.
