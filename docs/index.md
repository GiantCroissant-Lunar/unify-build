# UnifyBuild

A .NET build orchestration system built on [NUKE](https://nuke.build/) that provides composable component interfaces for building, packing, and publishing .NET, native (CMake), Rust, Go, Unity, Godot, and mobile projects — all driven by a single `build.config.json` file.

## Using UnifyBuild

- [Getting Started](guides/getting-started.md) — Installation and first build
- [Configuration Reference](reference/configuration-reference.md) — Every `build.config.json` property
- [Targets](reference/targets.md) — Every build target, its dependencies, and where it's available
- [CLI](reference/cli.md) — `init`, `validate`, `doctor`, `migrate`, and global parameters
- [Examples](examples/dotnet-library.md) — End-to-end project examples
- [Tooling](reference/tooling.md) — Unity package, VS Code extension, and metrics dashboard
- [Troubleshooting](guides/troubleshooting.md) — Common errors and fixes
- [Migration Guide](guides/migration-guide.md) — Moving a v1 config to v2

## Developing UnifyBuild

- [Architecture](architecture/index.md) — Component design and extension points
- [Extending UnifyBuild](guides/extending.md) — Writing a custom component interface
- [Releasing](guides/releasing.md) — Version bumps, tagging, NuGet, and OpenUPM onboarding
- [Agent Rules and Skills](guides/agent-rules.md) — `AGENTS.md` and the generated pointer files
- [Specifications](specs/index.md) — Feature requirements, design notes, and implementation tasks
- [Contributing](https://github.com/GiantCroissant-Lunar/unify-build/blob/main/CONTRIBUTING.md) — PR process, commit convention, and code style

---

[Archived material](archive/index.md) — superseded reviews, progress notes, and removed-behavior docs — is kept for maintainers but is not part of the published navigation.
