# Nuke Build Components

This directory contains reusable Nuke build components following the
[Nuke.Build component pattern](https://nuke.build/docs/sharing/build-components/).

These components are designed to be **generic** and configured via
`build/nuke/build.config.json` so they can be reused across projects.

## Components

### `IBuildConfig`

Provides strongly-typed access to `build/nuke/build.config.json`.

Key settings (all optional):

- `solutionPath` – Relative path to the main solution, e.g. `dotnet/MySolution.sln`.
- `sourceDir` – Source code directory (default: `"dotnet"`).
- `websiteDir` – Website directory (default: `"website"`).
- `frameworkDirs` – Framework project directories (default: `["framework"]`).
- `pluginDirs` – Plugin project directories (default: `["plugins"]`).
- `packPlugins` – Whether to pack plugins (default: `true`).
- `packFramework` – Whether to pack framework (default: `true`).
- `publishProjectPaths` – Relative paths to projects to publish.
- Local NuGet feed settings (optional):
  - `syncLocalNugetFeed`
  - `localNugetFeedRoot`
  - `localNugetFeedFlatSubdir`
  - `localNugetFeedHierarchicalSubdir`
  - `localNugetFeedBaseUrl`

### `IClean`

Provides a `Clean` target to clean build artifacts:

- Removes `bin` and `obj` directories under the configured `sourceDir`.
- Creates or cleans `build/_artifacts`.

### `IRestore`

Restores NuGet packages for the solution specified by
`config.solutionPath` in `build.config.json`.

- If `solutionPath` is not set or the file does not exist, the target
  prints a message and no-ops instead of failing.

### `ICompile`

Builds the solution specified by `config.solutionPath`.

- Depends on `IRestore`.
- Uses the `Configuration` parameter (default: `Debug`).
- No-ops safely if `solutionPath` is missing.

### `ITest`

Runs tests for the solution specified by `config.solutionPath`.

- Depends on `Compile`.
- Uses the same `Configuration` parameter.
- Runs `dotnet test` with `--no-build --no-restore`.
- No-ops safely if `solutionPath` is missing.

### `IVersioning`

Provides shared versioning and artifact root logic:

- `ArtifactsRoot` – base artifacts directory (`build/_artifacts`).
- `ArtifactsVersion` – resolved as:
  1. `BUILD_VERSION` environment variable (if set), otherwise
  2. latest git tag (`git describe --tags --abbrev=0`, if available), otherwise
  3. fallback `"0.0.0-local"`.
- `PublishDirectory` – `build/_artifacts/{ArtifactsVersion}`.
- `BuildLogsDirectory` – `build/_artifacts/{ArtifactsVersion}/build-logs`.

### `IPublish`

Publishes projects listed in `config.publishProjectPaths` to a
versioned artifacts directory using `IVersioning`.

- Artifacts root and version come from `IVersioning`.
- Per-project output: `build/_artifacts/{ArtifactsVersion}/{ProjectName}`.
- Logs per publish run into
  `build/_artifacts/{ArtifactsVersion}/build-logs/`.

If `publishProjectPaths` is empty, the target prints a message and
no-ops.

### `IPack`

Packs projects into NuGet packages and optionally syncs them to a local feed.

- Projects to pack:
  - Prefer `config.packProjectPaths` when non-empty.
  - Fallback to `config.publishProjectPaths` if `packProjectPaths` is empty.
- Output directory: `build/_artifacts/{ArtifactsVersion}/nuget`.
- If `syncLocalNugetFeed` is `true` and `localNugetFeedRoot` is set:
  - Copies all `.nupkg` files to a flat directory
    `localNugetFeedRoot/localNugetFeedFlatSubdir`.
  - Copies each package into a simple hierarchical directory
    `localNugetFeedRoot/localNugetFeedHierarchicalSubdir/{PackageFileNameWithoutExt}/`.

## Usage

In your `Build.cs`:

```csharp
class Build : NukeBuild,
    IBuildConfig,
    IClean,
    IRestore,
    ICompile,
    ITest,
    IVersioning,
    IPublish,
    IPack
{
    public static int Main () => Execute<Build>(x => ((ICompile)x).Compile);
}
```

Then create `build/nuke/build.config.json`:

```json
{
  "solutionPath": "dotnet/MySolution.sln",
  "publishProjectPaths": [
    "dotnet/MyApp/MyApp.csproj"
  ]
}
```

You can now run:

```bash
nuke Clean
nuke Restore
nuke Compile
nuke Test
nuke Pack
nuke Publish
```

All behavior is driven by configuration so these components can be
reused across repositories (including in satellite projects that copy
this `Components` folder from `lunar-snake-hub`).
