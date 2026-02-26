# Changelog

## 3.0.0

### Breaking Changes

- **v1 flat config deprecated**: The `hostsDir`, `pluginsDir`, `contractsDir`, and related `include*`/`exclude*` properties are deprecated. Use `projectGroups` instead. These properties still work but emit warnings and will be removed in v4.0.
- **`UnifyBuildBase` class deprecated**: Use the composable `IUnify*` component interfaces directly. `UnifyBuildBase` will be removed in v4.0.
- **`$schema` reference recommended**: New configs should include `"$schema": "./build.config.schema.json"` for IDE IntelliSense and validation.

### Added

- `dotnet unify-build migrate` command for automated v1 → v2 config migration with backup
- `dotnet unify-build validate` command for schema and semantic config validation
- `dotnet unify-build doctor` command for environment diagnostics with `--fix` support
- `dotnet unify-build init --wizard` interactive configuration wizard
- Rust (Cargo) and Go build support via `IUnifyRust` and `IUnifyGo` component interfaces
- Distributed build cache with configurable remote URL
- Build metrics and observability (JSON/CSV export)
- Advanced package management: multi-registry push, signing, SBOM generation
- External component loading from custom assemblies
- Parallel build optimization for independent project groups
- VS Code extension with IntelliSense, snippets, and tree view
- Build analytics dashboard
- Migration guide at `docs/migration-guide.md`

### Changed

- JSON Schema extended with `rustBuild`, `goBuild`, `performance`, `observability`, `packageManagement`, and `extensions` sections
- Enhanced error messages with error codes (UBxxx), line/column info, and docs links
- Improved native build support with vcpkg auto-detection and custom build commands

### Deprecated

- v1 config properties (`hostsDir`, `pluginsDir`, `contractsDir`, `includeHosts`, `excludeHosts`, `includePlugins`, `excludePlugins`, `includeContracts`, `excludeContracts`)
- `UnifyBuildBase` class — use `IUnify*` interfaces instead

## 2.0.0

- Added build config schema (`projectGroups`).
- `projectGroups` is now required; legacy v1 configs are no longer supported.
