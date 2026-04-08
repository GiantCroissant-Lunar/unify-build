# Implementation Plan: Automated JSON Schema Generation

## Overview

This plan implements automated JSON schema generation for UnifyBuild using QuickType. The implementation adds a NUKE build target that generates JSON schemas from C# configuration classes, packages them with NuGet distributions, and enables IDE autocomplete and validation for consumer projects.

## Tasks

- [ ] 1. Set up QuickType tool infrastructure
  - [x] 1.1 Create .config directory and tool manifest
    - Create .config/dotnet-tools.json with QuickType entry
    - Configure tool version and restore settings
    - _Requirements: 1.2_

  - [x] 1.2 Implement InstallQuickTypeTool target
    - Add NUKE target to install QuickType as local .NET tool
    - Implement idempotent installation check (skip if already installed)
    - Add error handling for network failures with clear error messages
    - _Requirements: 1.1, 1.3, 1.4_

  - [ ]* 1.3 Write unit tests for tool installation
    - Test tool installation succeeds
    - Test idempotent behavior (skip when already installed)
    - Test error handling for installation failures
    - _Requirements: 1.1, 1.4_

- [ ] 2. Implement schema generation core functionality
  - [x] 2.1 Create IUnifySchemaGeneration interface
    - Define interface with GenerateSchema target signature
    - Add XML documentation for interface and members
    - Place in appropriate namespace (UnifyBuild.Nuke)
    - _Requirements: 2.1, 2.2_

  - [x] 2.2 Implement GenerateSchemaFromCSharp function
    - Create function to execute QuickType CLI with proper arguments
    - Implement input validation (file exists, is .cs file)
    - Create output directory if it doesn't exist
    - Build QuickType command arguments (--src, --out, --lang schema, --top-level, --just-types)
    - Execute QuickType process and capture output
    - _Requirements: 2.1, 2.2, 2.3, 2.5_

  - [x] 2.3 Implement ValidateGeneratedSchema function
    - Parse schema file as JSON and validate structure
    - Check for required properties ($schema, type, properties)
    - Validate root type is "object"
    - Return validation result with specific error messages
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [x] 2.4 Implement GenerateSchema target
    - Add target that depends on InstallQuickTypeTool
    - Configure to run before Pack target
    - Call GenerateSchemaFromCSharp with BuildConfigJson.cs as input
    - Call ValidateGeneratedSchema to verify output
    - Add logging for success and failure cases
    - _Requirements: 2.1, 2.2, 2.4, 5.1, 5.2, 5.3_

  - [ ]* 2.5 Write property test for schema completeness
    - **Property 2: Schema Completeness**
    - **Validates: Requirements 2.1, 2.4**
    - Test that all public properties from BuildConfigJson and nested types appear in generated schema

  - [ ]* 2.6 Write property test for type mapping accuracy
    - **Property 4: Type Mapping Accuracy**
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6**
    - Test that C# types correctly map to JSON Schema types

  - [ ]* 2.7 Write unit tests for schema generation
    - Test schema generation from valid C# file
    - Test error handling for invalid input files
    - Test error handling for QuickType execution failures
    - Test schema validation logic
    - _Requirements: 2.2, 2.3, 4.1, 4.2, 4.3, 4.4_

- [x] 3. Checkpoint - Verify schema generation works
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 4. Configure NuGet package to include schema
  - [x] 4.1 Update UnifyBuild.Nuke.csproj with schema packaging
    - Add <None> element to include schema file from artifacts directory
    - Configure Pack="true" and PackagePath="contentFiles/any/any;content"
    - Set CopyToOutputDirectory="PreserveNewest"
    - Add Condition to check file exists before including
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x] 4.2 Update Pack target to depend on GenerateSchema
    - Ensure GenerateSchema runs before Pack
    - Verify schema file exists in artifacts before packing
    - _Requirements: 5.1, 5.3_

  - [ ]* 4.3 Write integration test for package contents
    - Test that packed NuGet package contains schema file
    - Test schema file is at correct path in package
    - Test schema file has correct metadata (CopyToOutputDirectory)
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [ ] 5. Implement error handling and diagnostics
  - [x] 5.1 Add comprehensive error handling to schema generation
    - Catch and log QuickType parsing errors with file/line info
    - Catch and log file I/O errors with path and permission details
    - Provide actionable error messages for common failures
    - Add manual installation instructions for tool installation failures
    - _Requirements: 12.1, 12.2, 12.3, 12.4_

  - [ ]* 5.2 Write unit tests for error scenarios
    - Test error handling for missing source file
    - Test error handling for invalid C# syntax
    - Test error handling for write permission errors
    - Test error messages are clear and actionable
    - _Requirements: 12.1, 12.2, 12.3, 12.4_

- [ ] 6. Create documentation and examples
  - [x] 6.1 Update README with schema usage instructions
    - Document how to add $schema reference to build.config.json
    - Explain schema file location and naming convention
    - Add troubleshooting section for common issues
    - Document schema update process when package is updated
    - _Requirements: 13.1, 13.3, 13.4_

  - [x] 6.2 Create example build.config.json with schema reference
    - Add example file showing correct $schema reference format
    - Include comments explaining the schema reference
    - Show example of valid configuration with autocomplete-friendly structure
    - _Requirements: 13.2_

  - [x] 6.3 Add XML documentation to public APIs
    - Document IUnifySchemaGeneration interface
    - Document GenerateSchema target
    - Document public functions and their parameters
    - _Requirements: 13.1_

- [x] 7. Integration testing and validation
  - [x] 7.1 Create test consumer project
    - Set up minimal test project that installs UnifyBuild.Nuke
    - Verify schema file is copied to project root after installation
    - Verify schema file is updated when package is updated
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 7.2 Write integration test for schema synchronization
    - **Property 10: Schema Synchronization with Source**
    - **Validates: Requirements 11.1, 11.2, 11.3, 11.4**
    - Test that modifying BuildConfigJson.cs and regenerating produces updated schema

  - [x] 7.3 Write integration test for end-to-end workflow
    - Test full build pipeline from schema generation to package creation
    - Verify schema is generated before Pack
    - Verify schema is included in package
    - Verify build fails if schema generation fails
    - _Requirements: 5.1, 5.2, 5.3, 6.1_

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- The implementation uses C# and NUKE build system
- QuickType is installed as a local .NET tool (not global)
- Schema generation happens at build time, not runtime
- Consumers manually add $schema reference to their build.config.json
- IDE autocomplete and validation work automatically once schema reference is added
