# CLI Reference

The `unify-build` CLI is distributed as a .NET tool and invoked as `dotnet unify-build`.

```bash
dotnet tool restore
dotnet unify-build <Target> [parameters]
```

Commands and build targets share one namespace — `init` and `Compile` are both NUKE targets and
are invoked the same way. Names are case-insensitive.

## Root directory discovery

The CLI does not need to be run from the repository root. On startup it walks **up** from the
current directory looking for either:

- `build.config.json`, or
- `build/build.config.json`

The first directory containing one becomes the build root. If neither is found in any ancestor
directory, the tool exits with an error rather than guessing.

## Global parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `--config <path>` | `string` | `build.config.json` | Path to the config file, relative to the build root or absolute |
| `--configuration <name>` | `string` | `Release` | Build configuration passed to MSBuild |

`--configuration` is provided by `IUnifyBuildConfig` and therefore applies to every compile, pack,
and publish target.

## `init`

Scaffold a new `build.config.json` by discovering projects in the repository.

```bash
dotnet unify-build init
dotnet unify-build init --template library
dotnet unify-build init --interactive
```

| Parameter | Type | Default | Description |
|---|---|---|---|
| `--template <name>` | `string` | — | Generate from a template: `library` or `application` |
| `--interactive` | `bool` | `false` | Run the configuration wizard with step-by-step prompts |
| `--wizard` | `bool` | `false` | Alias for `--interactive` |
| `--force` | `bool` | `false` | Overwrite an existing `build.config.json` |

Without `--template` or `--interactive`, `init` discovers projects and infers a configuration from
their metadata. An unknown template name is an error — only `library` and `application` are
supported.

The interactive wizard additionally probes for non-.NET technologies and offers matching config
sections when it finds them:

| Detected by | Adds |
|---|---|
| `CMakeLists.txt` | [`nativeBuild`](configuration-reference.md#native-build-configuration) |
| `Cargo.toml` | [`rustBuild`](configuration-reference.md#rust-build-configuration) |
| `Assets/` + `ProjectSettings/` | Unity configuration |

`init` refuses to overwrite an existing config unless `--force` is passed.

## `validate`

Validate `build.config.json` against the JSON Schema and run semantic checks.

```bash
dotnet unify-build validate
dotnet unify-build validate --config build/build.config.json
```

Validation runs in two stages:

1. **Schema validation** — structural conformance to `build.config.schema.json`.
2. **Semantic validation** — cross-checks against the repository, such as whether the directories
   referenced by project groups actually exist.

Errors and warnings are reported separately. The target **fails the build** when there is at least
one error; warnings alone still exit successfully.

## `doctor`

Diagnose the build environment and configuration.

```bash
dotnet unify-build doctor
dotnet unify-build doctor --fix
```

| Parameter | Type | Default | Description |
|---|---|---|---|
| `--fix` | `bool` | `false` | Automatically resolve fixable issues |

Checks include:

| Check | What it verifies |
|---|---|
| dotnet SDK | The SDK is installed and on `PATH` |
| Config Parsing | `build.config.json` exists and contains valid JSON |
| Schema Validation | The config conforms to the schema |
| Project Groups | At least one group is configured and its `sourceDir` directories exist |
| NUKE Global Tool | The NUKE global tool is installed (optional, recommended) |
| UnifyBuild Version | The installed tool version |

Each check reports `✓` pass, `⚠` warning, or `✗` fail, with a fix suggestion where one exists.
`doctor` exits non-zero when any check fails. When fixable issues remain and `--fix` was not
passed, it says so in the summary.

## `migrate`

Migrate a v1 `build.config.json` to the current v2 schema.

```bash
dotnet unify-build migrate
```

The v1 schema used domain-specific directory properties; v2 replaces them with generic
[project groups](configuration-reference.md#project-groups). `migrate` detects a v1 config by the
presence of any of `hostsDir`, `pluginsDir`, `contractsDir`, `includeHosts`, `excludeHosts`,
`includePlugins`, `excludePlugins`, `includeContracts`, `excludeContracts`.

| v1 property | v2 result |
|---|---|
| `hostsDir` | `projectGroups.executables` (action: `publish`) |
| `pluginsDir` | `projectGroups.libraries` (action: `pack`) |
| `contractsDir` | `projectGroups.contracts` (action: `pack`) |
| `includeHosts` / `excludeHosts` | `projectGroups.executables.include` / `.exclude` |
| `includePlugins` / `excludePlugins` | `projectGroups.libraries.include` / `.exclude` |
| `includeContracts` / `excludeContracts` | `projectGroups.contracts.include` / `.exclude` |

A backup is written to `build.config.json.bak` before any changes. If the config is already v2,
`migrate` reports that no changes were needed and writes no backup.

See the [Migration Guide](../guides/migration-guide.md) for the full walkthrough.

## Build targets

All other targets — `Compile`, `PackAll`, `PublishHosts`, `UnityExport`, `BuildGodot`, the mobile
targets, and so on — are documented in the [Targets Reference](targets.md).

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Success |
| non-zero | Target failure — validation errors, failing doctor checks, or a build/tool error |
