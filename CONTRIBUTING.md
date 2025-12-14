# Contributing to unify-build

Thanks for your interest in contributing to **unify-build**!

This repository contains build tooling (NUKE) and shared build configuration
loading utilities under `UnifyBuild.Nuke`.

## Project layout

- `src/UnifyBuild.Nuke/` – NUKE helper library + build config schema loader
- `build/nuke/` – NUKE build entrypoint for this repo
- `build/build.config.json` – build config used for dogfooding
- `docs/rfcs/` – design notes / RFCs

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

   - `docs/rfcs/rfc-0001-generic-build-schema.md`
   - `docs/SIMPLIFIED_V2_ONLY.md`

## Pull requests

- Keep PRs **small and focused** when possible.
- Describe the **motivation**, **approach**, and **any breaking changes**.
- If you change build schema / build behavior, please update relevant docs under `docs/`.

## Code style

- Follow existing C# style in the repository.
- Avoid introducing new dependencies unless clearly justified.

## Licensing

By submitting a contribution, you agree that your work will be licensed under the
same license as this project, the **MIT License** (see `LICENSE`).
