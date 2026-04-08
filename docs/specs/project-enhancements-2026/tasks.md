# Implementation Plan: UnifyBuild Project Enhancements 2026

## Overview

This plan implements 22 requirements across 6 phases, progressing from P0 foundation work through P2 ecosystem features. Each task builds incrementally on previous work, with checkpoints to validate progress. The implementation uses C# with xUnit + FsCheck for testing, targeting the existing `UnifyBuild.Nuke` and `UnifyBuild.Tool` projects.

## Tasks

- [ ] 1. Phase 1: Foundation (P0 Quick Wins)

  - [x] 1.1 Create ErrorCode enum and DiagnosticMessage record in UnifyBuild.Nuke
    - Create `dotnet/src/UnifyBuild.Nuke/Diagnostics/ErrorCode.cs` with error code enum (UB1xx config, UB2xx build, UB3xx tool, UB4xx schema)
    - Create `dotnet/src/UnifyBuild.Nuke/Diagnostics/DiagnosticMessage.cs` with structured error record (Code, Message, FilePath, Line, Column, Suggestion, DocsLink)
    - _Requirements: 5.1, 5.6, 5.11, 5.12_

  - [x] 1.2 Implement ErrorDiagnostics helper class
    - Create `dotnet/src/UnifyBuild.Nuke/Diagnostics/ErrorDiagnostics.cs` with factory methods for creating diagnostic messages from exceptions
    - Wrap `JsonException` with line/column info and docs link for config parse errors
    - Wrap `FileNotFoundException` with searched paths for missing config/project errors
    - Add verbose logging support for executed commands
    - _Requirements: 5.1, 5.2, 5.3, 5.7, 5.8, 5.9, 5.10_

  - [ ]* 1.3 Write property test for diagnostic messages (Property 9)
    - **Property 9: Diagnostic messages include error code and docs link**
    - Create FsCheck generator for `DiagnosticMessage` in `dotnet/tests/UnifyBuild.Nuke.Tests/Properties/Generators/DiagnosticGenerators.cs`
    - Verify all generated `DiagnosticMessage` instances have valid `ErrorCode` and non-empty `DocsLink`
    - **Validates: Requirements 5.6, 5.11, 5.12**

  - [ ]* 1.4 Write property test for error location info (Property 6)
    - **Property 6: Error messages include location information**
    - For any malformed JSON input, verify the resulting error contains line and column numbers
    - **Validates: Requirements 4.5, 5.1**

  - [x] 1.5 Implement InitCommand with project discovery
    - Create `dotnet/src/UnifyBuild.Nuke/Commands/InitCommand.cs` with `InitOptions`, `InitResult`, and `DiscoveredProject` records
    - Implement `DiscoverProjects()` to recursively find `.csproj` files, excluding `bin/`, `obj/`, `.git/`, `node_modules/`
    - Implement `Execute()` to generate `BuildJsonConfig` from discovered projects with `$schema` reference
    - Implement `GenerateFromTemplate()` for "library" and "application" templates
    - Implement `SerializeConfig()` to write config with comments
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.8, 3.9, 3.10_

  - [ ]* 1.6 Write property test for project discovery (Property 2)
    - **Property 2: Project discovery finds all .csproj files**
    - Create FsCheck generator for directory trees with `.csproj` files in `dotnet/tests/UnifyBuild.Nuke.Tests/Properties/Generators/DirectoryTreeGenerators.cs`
    - Verify discovery returns every `.csproj` and no files from excluded directories
    - **Validates: Requirements 3.2**

  - [ ]* 1.7 Write property test for init command config validity (Property 1)
    - **Property 1: Init command generates valid configuration**
    - For any set of discovered projects and build actions, verify `InitCommand.Execute()` produces a config that passes JSON Schema validation
    - **Validates: Requirements 3.1, 3.5**

  - [x] 1.8 Wire InitCommand into UnifyBuild.Tool
    - Add `Init` target to `dotnet/src/UnifyBuild.Tool/Build.cs` that delegates to `InitCommand`
    - Support `--interactive`, `--template`, and `--force` parameters
    - _Requirements: 3.1, 3.7_

  - [x] 1.9 Create initial documentation structure
    - Create `docs/getting-started.md` with installation instructions, minimal Build_Config example, and common commands
    - Create `docs/configuration-reference.md` documenting all `build.config.json` properties
    - Create `docs/troubleshooting.md` with common errors and fixes
    - Update `README.md` with getting-started guide and links to docs
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.12_

  - [x] 1.10 Create example documentation
    - Create `docs/examples/dotnet-library.md` with library project example
    - Create `docs/examples/dotnet-application.md` with application project example
    - Create `docs/examples/native-build.md` with CMake integration example
    - Create `docs/examples/unity-build.md` with Unity integration example
    - _Requirements: 2.8, 2.9, 2.10, 2.11_

- [x] 2. Checkpoint - Phase 1 validation
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 3. Phase 2: Quality & Automation (P0 + P1 Core)

  - [x] 3.1 Create CI workflow for pull requests
    - Create `.github/workflows/ci.yml` with PR trigger on `main`
    - Configure matrix build for `[windows-latest, ubuntu-latest, macos-latest]`
    - Add steps: restore → compile → test → validate schema → lint (`dotnet format --verify-no-changes`)
    - Configure NuGet package caching via `actions/cache`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.11, 1.12_

  - [x] 3.2 Create CD workflow for main branch
    - Create `.github/workflows/cd.yml` with push trigger on `main`
    - Add steps: restore → full build via `dotnet unify-build PackProjects` (dogfooding)
    - _Requirements: 1.5_

  - [x] 3.3 Create release workflow for version tags
    - Create `.github/workflows/release.yml` with tag trigger matching `v*`
    - Add GitVersion setup via `gitversion/setup@v1`
    - Add steps: build → pack → publish to NuGet.org → create GitHub Release
    - Generate SBOM via `dotnet-sbom-tool` (SPDX format) and attach to release
    - Generate changelog and include in release body
    - Store `NUGET_API_KEY` as GitHub secret reference
    - _Requirements: 1.5, 1.6, 1.7, 1.8, 1.9, 1.10_

  - [x] 3.4 Set up test project infrastructure
    - Create `dotnet/tests/UnifyBuild.Nuke.Tests/UnifyBuild.Nuke.Tests.csproj` with xUnit, FsCheck.Xunit, FluentAssertions, NSubstitute references
    - Create `dotnet/tests/UnifyBuild.Nuke.Tests/Fixtures/TestConfigFixtures.cs` with shared test data
    - Create `dotnet/tests/UnifyBuild.Nuke.Tests/Fixtures/TempDirectoryFixture.cs` with temp directory helper
    - Add test projects to `dotnet/UnifyBuild.sln`
    - _Requirements: 6.9, 6.10_

  - [x] 3.5 Implement unit tests for BuildContextLoader
    - Create `dotnet/tests/UnifyBuild.Nuke.Tests/Unit/BuildContextLoaderTests.cs`
    - Test config loading from valid JSON, invalid JSON, missing file, empty file
    - Test version resolution from GitVersion environment variables
    - Test project group parsing with various configurations
    - _Requirements: 6.1, 6.2, 6.11_

  - [x] 3.6 Implement unit tests for config parsing and schema validation
    - Create `dotnet/tests/UnifyBuild.Nuke.Tests/Unit/ConfigParsingTests.cs` for JSON deserialization edge cases
    - Test missing optional properties, extra properties, type mismatches
    - Create parameterized tests for Build_Config variations
    - _Requirements: 6.2, 6.3, 6.11_

  - [x] 3.7 Implement unit tests for InitCommand
    - Create `dotnet/tests/UnifyBuild.Nuke.Tests/Unit/InitCommandTests.cs`
    - Test project discovery with various directory structures
    - Test template generation for library and application templates
    - Test overwrite protection when config already exists
    - _Requirements: 6.4, 6.5_

  - [x] 3.8 Implement unit tests for error handling paths
    - Create `dotnet/tests/UnifyBuild.Nuke.Tests/Unit/ErrorDiagnosticsTests.cs`
    - Test diagnostic message creation from various exception types
    - Test error code assignment and docs link generation
    - _Requirements: 6.6_

  - [x] 3.9 Set up automated changelog generation
    - Configure conventional commit format validation in CI (PR check)
    - Add `cliff.toml` or equivalent config for `git-cliff` changelog generation
    - Create `CHANGELOG.md` in repository root
    - Add changelog generation step to release workflow
    - Document conventional commit format in `docs/contributing.md`
    - _Requirements: 15.1, 15.2, 15.3, 15.4, 15.5, 15.6, 15.7, 15.8, 15.9, 15.10_

  - [x] 3.10 Implement MigrateCommand for backward compatibility
    - Create `dotnet/src/UnifyBuild.Nuke/Commands/MigrateCommand.cs` with `MigrateResult` record
    - Implement `CreateBackup()` to save original config as `.bak`
    - Implement `ApplyMigrations()` to transform old config formats to current schema
    - Implement deprecation warning logging in `BuildContextLoader` for deprecated properties
    - _Requirements: 22.1, 22.3, 22.4, 22.5, 22.7, 22.8, 22.9, 22.10, 22.11, 22.12_

  - [x] 3.11 Wire MigrateCommand into UnifyBuild.Tool
    - Add `Migrate` target to `dotnet/src/UnifyBuild.Tool/Build.cs` that delegates to `MigrateCommand`
    - _Requirements: 22.8_

  - [ ]* 3.12 Write property test for migration (Property 17)
    - **Property 17: Migration produces valid config with backup**
    - For any valid old-format config, verify migration creates backup, produces valid config, and preserves project references
    - **Validates: Requirements 22.8, 22.9, 22.10**

  - [ ]* 3.13 Write property test for deprecation warnings (Property 16)
    - **Property 16: Deprecation warnings include migration guidance**
    - For any config with deprecated properties, verify loading produces warnings with property name and migration instruction
    - **Validates: Requirements 22.3, 22.4**

  - [ ]* 3.14 Write unit tests for MigrateCommand
    - Create `dotnet/tests/UnifyBuild.Nuke.Tests/Unit/MigrateCommandTests.cs`
    - Test backup creation, migration from v1 to v2, validation of migrated config
    - _Requirements: 6.7_

- [x] 4. Checkpoint - Phase 2 validation
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 5. Phase 3: Developer Experience (P1)

  - [x] 5.1 Implement ConfigValidator for schema and semantic validation
    - Create `dotnet/src/UnifyBuild.Nuke/Validation/ValidationResult.cs` with `ValidationResult`, `ValidationIssue`, and `ValidationSeverity` types
    - Create `dotnet/src/UnifyBuild.Nuke/Validation/ConfigValidator.cs`
    - Implement `ValidateSchema()` using NJsonSchema for JSON Schema validation
    - Implement `ValidateSemantic()` to verify project refs exist, source dirs exist, no duplicate projects
    - Return accumulated issues with severity, code, message, file path, line, and suggestion
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

  - [ ]* 5.2 Write property test for schema validation (Property 3)
    - **Property 3: Schema validation correctly classifies configs**
    - For any valid `BuildJsonConfig`, serializing to JSON and validating should pass
    - **Validates: Requirements 4.1**

  - [ ]* 5.3 Write property test for reference validation (Property 4)
    - **Property 4: Reference validation reports all missing paths**
    - For any config with project group references, verify all missing source dirs and projects are reported
    - **Validates: Requirements 4.2, 4.3, 5.2**

  - [ ]* 5.4 Write property test for duplicate detection (Property 5)
    - **Property 5: Duplicate project detection**
    - For any config with duplicate project names, verify each duplicate is reported
    - **Validates: Requirements 4.4**

  - [x] 5.5 Implement ValidateCommand
    - Create `dotnet/src/UnifyBuild.Nuke/Commands/ValidateCommand.cs`
    - Run schema validation and semantic validation, display results with line numbers and suggestions
    - Wire into `Build.cs` as `Validate` target
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

  - [x] 5.6 Implement DoctorCommand
    - Create `dotnet/src/UnifyBuild.Nuke/Commands/DoctorCommand.cs` with `DoctorResult` and `DoctorCheck` records
    - Implement checks: NUKE installed, dotnet SDK version, config valid, projects exist, source dirs exist, no duplicates, tool version
    - Implement `--fix` mode for auto-fixable issues
    - Wire into `Build.cs` as `Doctor` target
    - _Requirements: 4.6, 4.7, 4.8, 4.9, 4.10, 4.11_

  - [ ]* 5.7 Write property test for doctor fix suggestions (Property 7)
    - **Property 7: Doctor checks include fix suggestions**
    - For any `DoctorCheck` with Fail or Warning status, verify `FixSuggestion` is non-null and non-empty
    - **Validates: Requirements 4.10**

  - [ ]* 5.8 Write property test for doctor auto-fix (Property 8)
    - **Property 8: Doctor auto-fix resolves fixable issues**
    - For any auto-fixable issue, verify running with `autoFix: true` resolves the issue on subsequent check
    - **Validates: Requirements 4.11**

  - [ ]* 5.9 Write unit tests for ConfigValidator and DoctorCommand
    - Create `dotnet/tests/UnifyBuild.Nuke.Tests/Unit/ConfigValidatorTests.cs`
    - Create `dotnet/tests/UnifyBuild.Nuke.Tests/Unit/DoctorCommandTests.cs`
    - Test schema validation with valid/invalid configs, semantic validation with missing paths, doctor checks
    - _Requirements: 6.3, 6.5_

  - [x] 5.10 Create integration test project and end-to-end tests
    - Create `dotnet/tests/UnifyBuild.Integration.Tests/UnifyBuild.Integration.Tests.csproj`
    - Create `dotnet/tests/UnifyBuild.Integration.Tests/EndToEndBuildTests.cs` for complete build workflows
    - Create `dotnet/tests/UnifyBuild.Integration.Tests/EndToEndPackTests.cs` for pack workflows
    - Use temporary directories with real file system operations, cleanup via `IDisposable`
    - Add to solution
    - _Requirements: 7.1, 7.2, 7.3, 7.10, 7.11_

  - [ ]* 5.11 Write integration tests for dogfooding and schema deployment
    - Create `dotnet/tests/UnifyBuild.Integration.Tests/DogfoodingTests.cs` for smoke tests
    - Test JSON_Schema deployment workflow
    - _Requirements: 7.6, 7.12_

  - [x] 5.12 Set up security scanning and Dependabot
    - Create `.github/dependabot.yml` for NuGet and GitHub Actions dependency monitoring (weekly checks)
    - Add `dotnet list package --vulnerable --include-transitive` step to CI workflow with failure on critical
    - Create `SECURITY.md` with vulnerability reporting instructions and response process
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6, 11.7, 11.8, 11.9_

  - [x] 5.13 Implement ChangeDetection for incremental builds
    - Create `dotnet/src/UnifyBuild.Nuke/Performance/ChangeDetection.cs`
    - Implement `HasChanges()` comparing source file timestamps against build marker
    - Implement `UpdateMarker()` to create/update marker after successful build
    - Integrate with `IUnifyCompile` to skip compilation when no changes detected
    - _Requirements: 12.1, 12.2, 12.3_

  - [ ]* 5.14 Write property test for change detection (Property 12)
    - **Property 12: Change detection reflects file timestamps**
    - Verify `HasChanges()` returns true iff at least one source file is newer than the marker
    - **Validates: Requirements 12.2, 12.3**

  - [x] 5.15 Implement BuildCache for build output caching
    - Create `dotnet/src/UnifyBuild.Nuke/Performance/BuildCache.cs`
    - Implement `ComputeCacheKey()` from hash of project file + source files + config
    - Implement `TryGetCached()` and `Store()` for local cache in `build/_cache/`
    - _Requirements: 12.4, 12.5, 12.9_

  - [ ]* 5.16 Write property test for cache round trip (Property 13)
    - **Property 13: Build cache round trip**
    - Verify storing and retrieving with the same cache key produces identical file content
    - **Validates: Requirements 12.5**

  - [ ]* 5.17 Write unit tests for ChangeDetection and BuildCache
    - Create `dotnet/tests/UnifyBuild.Nuke.Tests/Unit/ChangeDetectionTests.cs`
    - Create `dotnet/tests/UnifyBuild.Nuke.Tests/Unit/BuildCacheTests.cs`
    - _Requirements: 6.5_

- [x] 6. Checkpoint - Phase 3 validation
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 7. Phase 4: Expansion (P1 + P2)

  - [x] 7.1 Implement IUnifyRust component interface
    - Create `dotnet/src/UnifyBuild.Nuke/IUnifyRust.cs` with `RustBuild` target
    - Add `RustBuildConfig` to `BuildJsonConfig` (CargoManifestDir, Profile, Features, TargetTriple, OutputDir, ArtifactPatterns)
    - Implement Cargo detection, `cargo build` execution with specified profile and features
    - Implement artifact collection from Cargo output to configured output directory
    - _Requirements: 8.6, 8.7, 8.8, 8.12, 8.13_

  - [x] 7.2 Implement IUnifyGo component interface
    - Create `dotnet/src/UnifyBuild.Nuke/IUnifyGo.cs` with `GoBuild` target
    - Add `GoBuildConfig` to `BuildJsonConfig` (GoModuleDir, BuildFlags, OutputBinary, OutputDir, EnvVars)
    - Implement Go detection, `go build` execution with specified flags and env vars (GOOS, GOARCH)
    - Implement artifact collection to configured output directory
    - _Requirements: 8.9, 8.10, 8.11, 8.12, 8.13_

  - [x] 7.3 Enhance IUnifyNative with vcpkg detection and custom commands
    - Add vcpkg toolchain auto-detection to existing `IUnifyNative`
    - Add support for custom build commands in native build config
    - Add platform-specific native build configuration support
    - _Requirements: 8.5, 8.13, 8.14_

  - [ ]* 7.4 Write property test for vcpkg detection (Property 10)
    - **Property 10: Vcpkg toolchain detection**
    - Verify `TryDetectVcpkgToolchain()` returns path when vcpkg.cmake exists, null otherwise
    - **Validates: Requirements 8.5**

  - [ ]* 7.5 Write property test for artifact collection (Property 11)
    - **Property 11: Artifact collection copies matching files**
    - Verify `CollectArtifacts()` copies exactly the files matching patterns and no others
    - **Validates: Requirements 8.12**

  - [x] 7.6 Update JSON Schema for new build configurations
    - Extend `build.config.schema.json` with `$defs` for `rustBuild`, `goBuild`, `performance`, `observability`
    - All new properties optional to maintain backward compatibility
    - Regenerate schema if using `IUnifySchemaGeneration`
    - _Requirements: 8.6, 8.9, 12.4, 14.1_

  - [x] 7.7 Create Unity build documentation and integration test
    - Enhance `docs/examples/unity-build.md` with package mapping examples, project structure requirements, target framework guidance
    - Add Unity_Build validation for project path and target framework compatibility
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.7, 9.8, 9.9, 9.10_

  - [ ]* 7.8 Write integration tests for native build workflows
    - Create `dotnet/tests/UnifyBuild.Integration.Tests/NativeBuildIntegrationTests.cs`
    - Test CMake, Rust, and Go build workflows with mock project structures
    - _Requirements: 7.4, 7.5_

  - [x] 7.9 Implement BuildMetrics for observability
    - Create `dotnet/src/UnifyBuild.Nuke/Performance/BuildMetrics.cs`
    - Track build duration per target, compilation time per project, cache hit/miss rates
    - Implement JSON and CSV export for metrics reports
    - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5, 14.6, 14.7_

  - [ ]* 7.10 Write property test for metrics invariants (Property 14)
    - **Property 14: Metrics invariants**
    - Verify total duration >= 0, target durations >= 0, sum of targets <= total, cache hits + misses = total lookups
    - **Validates: Requirements 14.1, 14.2, 14.3, 14.5**

  - [ ]* 7.11 Write property test for metrics serialization (Property 15)
    - **Property 15: Metrics serialization round trip**
    - Verify JSON serialize/deserialize round trip preserves all values
    - **Validates: Requirements 14.6, 14.7**

  - [x] 7.12 Create extensibility documentation
    - Create `docs/architecture.md` explaining component design and Build_Context extension points
    - Create `docs/contributing.md` with contribution guidelines for extending Component_Interfaces
    - Document how to create custom Component_Interfaces with Docker and Terraform examples
    - Document how to add custom Build_Config properties and test custom components
    - _Requirements: 13.1, 13.2, 13.3, 13.4, 13.5, 13.8, 13.9, 13.10, 2.13, 2.14_

- [x] 8. Checkpoint - Phase 4 validation
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Phase 5: Strategic Enhancements (P2)

  - [x] 9.1 Implement interactive configuration wizard
    - Enhance `InitCommand` with `Spectre.Console` multi-step wizard mode via `--wizard` flag
    - Implement repository structure detection and Project_Group suggestions
    - Implement multi-select project picker, build action selector with descriptions
    - Detect CMakeLists.txt for native build config, Unity project for Unity config
    - Add inline help, real-time validation, and config preview before saving
    - _Requirements: 16.1, 16.2, 16.3, 16.4, 16.5, 16.6, 16.7, 16.8, 16.9, 16.11_

  - [x] 9.2 Set up documentation site with docfx
    - Configure `docfx` or `mkdocs` to generate static site from `docs/` markdown
    - Add search functionality, mobile-responsive layout, version selector
    - Create GitHub Actions workflow to deploy to GitHub Pages on docs changes
    - _Requirements: 17.1, 17.2, 17.3, 17.4, 17.5, 17.6, 17.7, 17.8, 17.9_

  - [x] 9.3 Create community examples repository structure
    - Create example projects: .NET library, .NET application, microservices, native build, Unity build, monorepo, multi-target framework
    - Add README files explaining each example
    - Add CI validation for all examples
    - _Requirements: 19.1, 19.2, 19.3, 19.4, 19.5, 19.6, 19.7, 19.8, 19.9, 19.10, 19.11_

  - [x] 9.4 Implement advanced package management
    - Extend `BuildJsonConfig` with `PackageManagementConfig` (registries, signing, SBOM, retention)
    - Implement multi-registry push in `IUnifyPack` with registry-specific auth
    - Implement NuGet package signing support
    - Implement SBOM generation (SPDX/CycloneDX) for packed packages
    - Implement package retention policy for local feed cleanup
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7, 10.8, 10.9, 10.10, 10.11, 10.12_

  - [x] 9.5 Implement optional telemetry
    - Add opt-in telemetry config to `ObservabilityConfig` (disabled by default)
    - Implement anonymous data collection respecting user privacy
    - Document what telemetry data is collected in `docs/telemetry.md`
    - _Requirements: 14.8, 14.9, 14.10, 14.11, 14.12_

  - [x] 9.6 Add dependency license scanning
    - Add `dotnet list package --include-transitive` license check to CI
    - Generate warnings for incompatible licenses
    - _Requirements: 11.10, 11.11_

- [x] 10. Checkpoint - Phase 5 validation
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Phase 6: Ecosystem (P2)

  - [x] 11.1 Create VS Code extension scaffold
    - Create TypeScript VS Code extension project with package.json, tsconfig
    - Implement enhanced IntelliSense for Build_Config files (beyond `$schema` reference)
    - Implement code snippets for common Build_Config patterns
    - Implement tree view for Project_Groups
    - Implement command palette integration for init, validate, doctor commands
    - Implement hover documentation for Build_Config properties
    - _Requirements: 18.1, 18.2, 18.3, 18.4, 18.5, 18.6, 18.7, 18.8, 18.9_

  - [x] 11.2 Implement build analytics dashboard
    - Create simple HTML/JS dashboard reading JSON metrics files
    - Display build duration trends, success/failure rates, cache hit rates, slowest targets
    - Support filtering by date range, Build_Target, Project_Group
    - Support CSV export and authentication for team access
    - _Requirements: 21.1, 21.2, 21.3, 21.4, 21.5, 21.6, 21.7, 21.8, 21.9, 21.10, 21.11, 21.12_

  - [x] 11.3 Implement external component loading
    - Add support for loading custom components from external assemblies in `BuildContextLoader`
    - Validate interface implementation when loading custom components
    - _Requirements: 13.6, 13.7_

  - [x] 11.4 Add distributed build cache support
    - Extend `BuildCache` with distributed cache upload/download via configurable URL
    - Add retry with backoff for network failures
    - Document caching configuration and best practices in `docs/caching.md`
    - Add cache statistics to build output
    - _Requirements: 12.6, 12.7, 12.8, 12.10, 12.11, 12.12_

  - [x] 11.5 Add parallel build optimization
    - Ensure independent project groups can build in parallel without shared state
    - Optimize project discovery for large repositories
    - _Requirements: 12.9, 12.10_

  - [x] 11.6 Create migration guide and breaking changes documentation
    - Create `docs/migration-guide.md` for v1 → v2 schema migration
    - Document all breaking changes in CHANGELOG.md
    - _Requirements: 2.7, 22.6, 22.11_

- [x] 12. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation between phases
- Property tests validate the 17 correctness properties from the design document
- The project uses C# with xUnit + FsCheck for testing
- All new config properties are optional to maintain backward compatibility (Requirement 22)
- Phase 1-3 cover all P0 and core P1 requirements; Phases 4-6 cover expansion and P2 features
