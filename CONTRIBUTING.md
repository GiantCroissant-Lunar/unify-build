# Contributing to unify-build

Thanks for your interest in contributing to **unify-build**!

This repository contains build tooling (NUKE) and shared build configuration
loading utilities under `UnifyBuild.Nuke`.

## Project layout

- `dotnet/src/UnifyBuild.Nuke/` – NUKE helper library + build config schema loader
- `dotnet/src/UnifyBuild.Tool/` – the `dotnet unify-build` CLI
- `dotnet/tests/` – unit, integration, and packaging tests
- `build/nuke/` – NUKE build entrypoint for this repo
- `build/build.config.json` – build config used for dogfooding
- `docs/architecture/rfcs/` – design notes / RFCs

## Documentation

- [Architecture](docs/architecture/index.md) – component interfaces, config resolution, project structure
- [Extending UnifyBuild](docs/guides/extending.md) – step-by-step walkthrough for adding a component interface
- [Configuration Reference](docs/reference/configuration-reference.md) – every `build.config.json` property
- [Targets](docs/reference/targets.md) – every build target and its dependencies
- [Agent Rules and Skills](docs/guides/agent-rules.md) – why `CLAUDE.md` is generated and what to edit instead

If you change build schema or build behavior, update the relevant pages under `docs/`. The
configuration reference and the target catalog are the two that go stale fastest.

## Development workflow

1. **Build (Compile)**

   From the repo root:

   - `task nuke:compile`

2. **Pack NuGet package**

   From the repo root:

   - `task nuke:pack-projects`

   This produces packages under:

   - `build/_artifacts/local/nuget/`

3. **Config schema**

   The build config schema requires `projectGroups`.

   See:

   - `docs/architecture/rfcs/rfc-0001-generic-build-schema.md`
   - `docs/archive/reviews/SIMPLIFIED_V2_ONLY.md`

## Pull requests

- Keep PRs **small and focused** when possible.
- Describe the **motivation**, **approach**, and **any breaking changes**.
- If you change build schema / build behavior, please update relevant docs under `docs/`.
- PR titles **must** follow the conventional commit format (see below). CI will reject PRs with non-conforming titles.

## Commit convention

This project uses [Conventional Commits](https://www.conventionalcommits.org/) to
drive automated changelog generation and semantic versioning.

### Format

```
type(scope): description

[optional body]

[optional footer(s)]
```

### Types

| Type       | Description                                          |
|------------|------------------------------------------------------|
| `feat`     | A new feature                                        |
| `fix`      | A bug fix                                            |
| `docs`     | Documentation only changes                           |
| `refactor` | Code change that neither fixes a bug nor adds a feature |
| `perf`     | A code change that improves performance              |
| `test`     | Adding or updating tests                             |
| `ci`       | Changes to CI configuration files and scripts        |
| `build`    | Changes that affect the build system or dependencies |
| `chore`    | Other changes that don't modify src or test files    |
| `style`    | Code style changes (formatting, semicolons, etc.)    |

### Scope (optional)

A scope provides additional context, e.g. `feat(config): add Rust build support`.

Common scopes: `config`, `cli`, `native`, `unity`, `docs`, `ci`, `schema`.

### Breaking changes

Indicate breaking changes by adding `!` after the type/scope or by including a
`BREAKING CHANGE:` footer:

```
feat(config)!: remove deprecated v1 schema support

BREAKING CHANGE: v1 build.config.json files are no longer supported.
Run `dotnet unify-build migrate` to upgrade.
```

### Examples

```
feat(cli): add doctor command for config diagnostics
fix(config): handle missing projectGroups gracefully
docs: update getting-started guide with init command
refactor(native): extract vcpkg detection into helper
perf: cache project discovery results
test: add property tests for schema validation
ci: add commit message validation to PR checks
chore: update NuGet dependencies
```

## Code style

- Follow existing C# style in the repository.
- Avoid introducing new dependencies unless clearly justified.

## Licensing

By submitting a contribution, you agree that your work will be licensed under the
same license as this project, the **MIT License** (see `LICENSE`).
