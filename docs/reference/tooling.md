# Tooling

Beyond the `UnifyBuild.Nuke` library and the `unify-build` CLI, the repository ships three
supporting surfaces. None of them are required to build a project — each is optional, and each
lives alongside the code it supports.

| Surface | Location | Distribution |
|---|---|---|
| [Unity editor package](#unity-editor-package) | `unity/com.unifybuild.editor` | UPM / OpenUPM |
| [VS Code extension](#vs-code-extension) | `vscode-extension/` | `.vsix` (built in CI) |
| [Metrics dashboard](#metrics-dashboard) | `dashboard/` | Static files, opened locally |

## Unity editor package

`com.unifybuild.editor` provides the Unity-side half of Unity automation. The .NET layer decides
*what* to build and drives the editor; this package is *what the editor runs*.

It provides:

- `UnifyBuild.Editor.BuildScript.Build`, plus convenience wrappers for desktop and mobile targets
- a stable `-executeMethod` surface for Unity batch-mode builds
- the bridge used by `dotnet unify-build` export and packaging flows

`UnifyBuild.Editor.BuildScript.Build` is the default value of
[`unityExport.executeMethod`](configuration-reference.md#unity-export-configuration) — installing
this package is what makes that default work. Override `executeMethod` if you supply your own
entrypoint.

The split is deliberate: Unity export orchestration stays in the .NET layer because it runs
*outside* the editor, while editor assets stay out of the NuGet packages.

### Versioning

The package version tracks the repository release line — `package.json` is expected to match the
release tag and the NuGet package version for the same release. OpenUPM submission metadata lives
in `openupm/com.unifybuild.editor.yml`.

Until the OpenUPM publication path is finalized, `unity/com.unifybuild.editor` is the source of
truth for local or Git-based UPM consumption.

## VS Code extension

Editor support for `build.config.json`. It activates automatically when a workspace contains a
`build.config.json` anywhere in the tree.

| Feature | Detail |
|---|---|
| Schema validation | Bundles `build.config.schema.json` and binds it to `build.config.json` — no `$schema` property needed |
| Hover documentation | Inline docs for any config property |
| Snippets | `unifybuild-config`, `unifybuild-project-group`, `unifybuild-native-build` |
| Tree view | Project groups from `build.config.json`, in the Explorer sidebar |
| Commands | `UnifyBuild: Init`, `UnifyBuild: Validate Config`, `UnifyBuild: Doctor` |

The commands shell out to the CLI, so they require the [.NET SDK](https://dotnet.microsoft.com/download)
8.0+ and `UnifyBuild.Tool` installed as a dotnet tool. The schema, hover, and snippet features work
without either.

### Building it

```bash
cd vscode-extension
npm install
npm run check
npm run package
```

Press ++f5++ in VS Code to launch the Extension Development Host. CI compiles, lints, and packages
a `.vsix` on any change under `vscode-extension/`.

!!! note "Bundled schema can lag"
    The extension carries its own copy of the schema at `vscode-extension/schemas/`. It is
    regenerated from `BuildJsonConfig` by the [`GenerateSchema`](targets.md#schema-generation-iunifyschemageneration)
    target — if a newly added config property does not autocomplete, that copy is stale.

## Metrics dashboard

A dependency-free single-page app that visualizes the JSON reports written when
[`observability.enableMetrics`](configuration-reference.md#observability-configuration) is on.

Open `dashboard/index.html` in any modern browser — no build step and no server. Load one or more
JSON metrics files with the **Load Metrics** button or by dragging them in.

| View | Shows |
|---|---|
| Duration trend | Build duration over time |
| Success/failure rate | Passing vs. failing builds |
| Cache hit rate | Cache efficiency over time |
| Slowest targets | Top 10 slowest targets by average duration |
| Project compilation | Average compilation time per project |

Date-range, target, and project filters apply across all views, and the filtered set can be
exported to CSV.

### Input format

The dashboard reads what `BuildMetrics.ExportJson()` writes:

```json
{
  "timestamp": "2026-01-15T10:30:00+00:00",
  "totalDuration": "0.00:02:15.1234567",
  "targetDurations": { "Compile": "0.00:01:05.0000000", "Pack": "0.00:00:45.0000000" },
  "projectDurations": { "MyApp.csproj": "0.00:00:30.0000000" },
  "cacheHits": 5,
  "cacheMisses": 2,
  "cacheHitRate": 0.714,
  "success": true
}
```

A single file may also contain an array of reports (`[{...}, {...}]`). Both camelCase and
PascalCase property names are accepted.

By default, metrics are written to `build/_metrics` — see
[`observability.metricsOutputDir`](configuration-reference.md#observability-configuration) to
change that, and [Telemetry](telemetry.md) for what each field means.

### Hosting

The dashboard is a static client-side viewer with **no built-in authentication**. Loaded JSON is
processed in the browser and never uploaded. If you host it somewhere shared and the metrics are
sensitive, put real hosting-layer auth in front of it — reverse proxy, SSO, VPN, or an
authenticated internal portal.

!!! warning "Chart.js is loaded from a CDN"
    `index.html` pulls Chart.js from `cdn.jsdelivr.net`. The page needs network access on first
    load and will not render charts fully offline or in an air-gapped environment. Vendor the
    script locally if that matters for your setup.
