# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
## [0.1.0] - 2026-05-19

### Added

- **build-config**: make schema versionless and add ADR
- **godot**: leave extracted .app bundle alongside .zip on macOS
- **pack**: add SyncLocalFeed target to IUnifyPack
- **tool**: add UnifyBuild.Tool project
- add new dotnet project structure
- Add JSON schema support for build.config.json
- add error diagnostics with structured error codes and messages
- add InitCommand with project discovery and template generation
- add MigrateCommand and changelog/contributing infrastructure
- add ConfigValidator and ValidateCommand for schema/semantic validation
- add DoctorCommand for environment health checks
- add incremental build support with change detection and build cache
- add Rust and Go build support, enhance native build with vcpkg
- add BuildMetrics for build observability and reporting
- update BuildConfigJson, schema, and Build.cs for phases 1-4
- add interactive ConfigWizard with Spectre.Console
- add advanced package management with signing, SBOM, and retention
- add optional anonymous telemetry with documentation
- add community example projects with CI validation
- add VS Code extension scaffold with IntelliSense and snippets
- add build analytics dashboard with Chart.js
- add distributed build cache, parallel orchestration, and extensibility
- add IUnifyGodot interface and BuildGodot target
- add Fastlane integration for iOS/Android mobile builds
- add IUnifyMobile, IUnifyUnityExport interfaces and mobile/unity export pipeline
- add UnifyBuild.Editor Unity package with BuildScript for automated exports
- add Unity and Godot sample apps with build.config.json
### Changed

- **core**: introduce UnifyBuild interfaces and base class updates
- remove old src structure and move to dotnet folder
- move tests to dotnet/samples
- use OutputDir from GodotBuildContext instead of hardcoded artifact paths
- move internal samples into fixtures
- simplify the static dashboard
### Documentation

- **skills**: add skill-creator from Anthropic
- **skills**: add dotnet-build skill for UnifyBuild project
- update RFCs and inbox documents
- add dotnet tool migration progress documentation
- update README and Taskfile for new structure
- add getting-started guide, configuration reference, and examples
- add architecture documentation and extensibility guide
- add documentation site, migration guide, and mkdocs config
- reorganize specs and archive content
- avoid credential-like wording in specs
### Fixed

- **compile**: propagate UnifyConfig.Version to DotNetBuild calls
- **precommit**: point dotnet-format at dotnet/UnifyBuild.sln
- **version**: drive UnifyBuild.{Tool,Nuke} version via GitVersion + gate schema-gen
- write schema to build/_artifacts/ without version subfolder
- remove hardcoded artifactsVersion, use versionEnv from GitVersion
### Other

- **agent**: scaffold .agent infrastructure and ignore generated outputs
- **ci**: add dotnet format to pre-commit hooks
- **git**: remove .agent from gitignore to enable skill sharing
- **infra**: update build configuration and project structure
- **repo**: add templates and docs
- **taskfile**: self-bootstrap build.config.schema.json in tool:bootstrap-local
- **tooling**: bump gitversion tool to 6.5.1
- bootstrap unify-build build tooling
- configure hub consumer scaffolding
- add hub npm dependency
- update build configuration and scripts
- add repomix and pre-commit configuration
- add ruff linter and formatter to pre-commit
- add build props and targets, update tasks
- add CI/CD workflows and Dependabot configuration
- add project enhancements 2026 spec files
- update CHANGELOG and .gitignore for phases 5-6
- update GitVersion to 6.6.0 and fix config for v6 (Mainline strategy)
- update .gitignore for Unity, Godot, build artifacts, and Fastlane
- misc build config and schema updates
- validate and package the VS Code extension
- ignore local env files
- publish packages to central local feed
- fix whitespace in IUnifySchemaGeneration.cs
- add gitversion.sh wrapper for Git Bash
### Testing

- hub hook verification
- hook verification
- add test infrastructure and unit tests for core components
- add integration tests for end-to-end build and pack workflows
- add unit tests for phase 5-6 components
### Release

- align distribution and packaging metadata
