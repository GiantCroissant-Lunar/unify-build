# Example: .NET Application Project

Build and publish a .NET application (API + worker service) using UnifyBuild.

## Project Structure

```
my-app/
├── build.config.json
├── build.config.schema.json
├── src/
│   ├── MyApp.Api/
│   │   ├── MyApp.Api.csproj
│   │   └── Program.cs
│   ├── MyApp.Worker/
│   │   ├── MyApp.Worker.csproj
│   │   └── Program.cs
│   └── MyApp.Shared/
│       ├── MyApp.Shared.csproj
│       └── Models.cs
├── tests/
│   └── MyApp.Api.Tests/
│       └── MyApp.Api.Tests.csproj
└── MyApp.sln
```

## Configuration

```json
{
  "$schema": "./build.config.schema.json",
  "solution": "MyApp.sln",
  "projectGroups": {
    "hosts": {
      "sourceDir": "src",
      "action": "publish",
      "include": ["MyApp.Api", "MyApp.Worker"]
    },
    "libraries": {
      "sourceDir": "src",
      "action": "compile",
      "include": ["MyApp.Shared"]
    }
  }
}
```

This config defines two groups:

- **hosts** — the executable projects, published as self-contained deployables
- **libraries** — shared code that only needs to compile (it's referenced by the hosts)

## Commands

### Scaffold the config

```bash
dotnet unify-build init --template application
```

### Compile everything

```bash
dotnet unify-build Compile
```

### Publish executables

```bash
dotnet unify-build PublishHosts --configuration Release
```

### Expected output

```
═══════════════════════════════════════
Target: Compile
═══════════════════════════════════════
  Building MyApp.Shared...
  Building MyApp.Api...
  Building MyApp.Worker...
  ✓ Compile completed

═══════════════════════════════════════
Target: PublishHosts
═══════════════════════════════════════
  Publishing MyApp.Api → build/_artifacts/1.0.0/MyApp.Api/
  Publishing MyApp.Worker → build/_artifacts/1.0.0/MyApp.Worker/
  ✓ PublishHosts completed
```

Published output lands in `build/_artifacts/{version}/{projectName}/`.

## Mixed Actions

You can combine `pack` and `publish` in the same config. For example, if `MyApp.Shared` should also ship as a NuGet package:

```json
{
  "$schema": "./build.config.schema.json",
  "solution": "MyApp.sln",
  "projectGroups": {
    "hosts": {
      "sourceDir": "src",
      "action": "publish",
      "include": ["MyApp.Api", "MyApp.Worker"]
    },
    "packages": {
      "sourceDir": "src",
      "action": "pack",
      "include": ["MyApp.Shared"]
    }
  }
}
```

Running `PackProjects` packs the `packages` group; running `PublishHosts` publishes the `hosts` group. `Compile` builds everything.
