# Specifications

This section collects longer-form planning and implementation specs that were previously kept under `.kiro/specs`.

Use these documents for feature-level requirements, design notes, and execution checklists that sit between RFCs and day-to-day implementation work.

!!! warning "These are plans, not descriptions of current behavior"
    Specs describe what was **intended** at the time of writing. Some of it shipped, some of it
    changed shape during implementation, and some has not been built yet. Do not read a spec as a
    statement of how UnifyBuild works today.

    For current behavior, use the [Configuration Reference](../reference/configuration-reference.md),
    the [Targets Reference](../reference/targets.md), and [Architecture](../architecture/index.md).
    Each spec's `tasks.md` is the live checklist and the authoritative view of what is done.

## Current Specs

### Automated JSON Schema Generation

Covers the schema generation pipeline, package inclusion, and IDE validation workflow.

- [Requirements](automated-json-schema-generation/requirements.md) · [Design](automated-json-schema-generation/design.md) · [Tasks](automated-json-schema-generation/tasks.md)
- **Status:** in progress — 18 of 29 tasks checked as of 2026-07-28.

!!! note "The implementation diverged from this spec"
    The requirements and design describe generating the schema with **QuickType** via `npx`. The
    shipped implementation uses **NJsonSchema** reflection instead — see
    `BuildConfigSchemaGenerator` and the [`GenerateSchema` target](../reference/targets.md#schema-generation-iunifyschemageneration).

    The QuickType path still appears in `dotnet/tests/UnifyBuild.Package.Tests/SchemaSynchronizationTests.cs`,
    which shells out to `npx quicktype`. Treat the tool choice in this spec as historical.

### Project Enhancements 2026

Tracks the broader roadmap for CI, DX, testing, native build support, and related platform work across 12 areas.

- [Requirements](project-enhancements-2026/requirements.md) · [Design](project-enhancements-2026/design.md) · [Tasks](project-enhancements-2026/tasks.md)
- **Status:** in progress — 51 of 77 tasks checked as of 2026-07-28.

## Relationship to Other Docs

| Question | Where to look |
|---|---|
| How does UnifyBuild behave today? | [Reference](../reference/configuration-reference.md) and [Architecture](../architecture/index.md) |
| Why is it shaped this way? | [ADRs and RFCs](../architecture/rfcs/rfc-0001-generic-build-schema.md) |
| What is planned or in flight? | This section |
| What did we used to do? | [Archive](../archive/index.md) |
