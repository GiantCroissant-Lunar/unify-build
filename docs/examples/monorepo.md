# Example: Monorepo

Manage multiple services and shared libraries in a single repository, packing the libraries as
NuGet packages and publishing the services as executables.

Runnable source: [`examples/monorepo/`](https://github.com/GiantCroissant-Lunar/unify-build/tree/main/examples/monorepo).

## Project Structure

```
monorepo/
├── build.config.json
├── Monorepo.sln
├── libs/                      # Shared libraries → packed as NuGet
│   ├── Common/
│   │   └── Result.cs
│   └── Logging/
│       └── Logger.cs
└── services/                  # Deployable services → published
    ├── OrderService/
    │   └── Program.cs
    └── InventoryService/
        └── Program.cs
```

## Configuration

```json
{
  "$schema": "../../build/build.config.schema.json",
  "solution": "Monorepo.sln",
  "projectGroups": {
    "packages": {
      "sourceDir": "libs",
      "action": "pack",
      "include": ["Common", "Logging"]
    },
    "services": {
      "sourceDir": "services",
      "action": "publish",
      "include": ["OrderService", "InventoryService"]
    }
  }
}
```

The key idea is **two project groups with different actions**. `sourceDir` scopes discovery to a
subtree, and `action` decides what happens to everything found there:

| Group | `sourceDir` | `action` | Target that runs it |
|---|---|---|---|
| `packages` | `libs` | `pack` | `PackProjects` |
| `services` | `services` | `publish` | `PublishHosts` |

Group names are arbitrary labels — `packages` and `services` carry no special meaning. Only
`action` determines behavior.

## Commands

### Compile everything

```bash
dotnet unify-build Compile
```

### Pack the shared libraries

```bash
dotnet unify-build PackProjects --configuration Release
```

### Publish the services

```bash
dotnet unify-build PublishHosts --configuration Release
```

Packages land in `build/_artifacts/{version}/nuget/`; published services land in
`build/_artifacts/{version}/`.

## When to Use This Pattern

- Multiple microservices sharing common code
- Internal NuGet packages consumed by several services in the same repo
- Teams working on different services within one repository

## Tips

- `include` is matched against project names without the `.csproj` extension. Omit it to take
  everything under `sourceDir`; discovery already skips `bin/`, `obj/`, `.git/`, and
  `node_modules/`.
- Add a third group with `action: "compile"` for projects that should build but produce no
  deployable output — benchmarks, samples, or internal tools.
- Per-group `properties` let you pass different MSBuild properties to libraries and services
  without touching the `.csproj` files. See the
  [Configuration Reference](../reference/configuration-reference.md#project-group-properties).
- Run `dotnet unify-build SyncLatestArtifacts` after publishing to mirror the versioned output
  folder to `build/_artifacts/latest`.
