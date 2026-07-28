# ADR-0001: Build config schema is unversioned (v2 becomes canonical)

## Status

Accepted

## Context

`unify-build` originally had a build configuration JSON schema that later evolved into a second iteration (commonly referred to as "v2"). The earlier schema ("v1") is no longer present in this repository and is no longer supported by the loader.

At this point, keeping explicit "v1"/"v2" terminology in file names and documentation is more confusing than helpful:

- There is only one supported schema.
- Consumers cannot choose between versions.
- The most important contract is the shape of `build.config.json`, not its historical version label.

## Decision

Treat the remaining schema (historically "v2") as the canonical, versionless schema.

Implications in this repo:

- The canonical config file name is `build/build.config.json`.
- The canonical schema file name is `build/build.config.schema.json`.
- Documentation and examples should avoid "v2-only" phrasing and instead describe the schema as simply "the build config schema".

## Consequences

- Older (v1) config files will not load.
- Downstream repositories must migrate their configs to the current schema.
- If we introduce a future breaking schema change, we will create a new ADR and use explicit versioning again (for example, by introducing a `schemaVersion` field or new file naming), rather than retroactively calling the current schema "v2" everywhere.
