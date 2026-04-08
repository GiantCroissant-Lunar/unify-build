# Requirements Document: Automated JSON Schema Generation

## Introduction

This document specifies the requirements for automated JSON schema generation in UnifyBuild. The system generates JSON schemas from C# configuration classes using QuickType, packages them with NuGet distributions, and enables IDE autocomplete and validation for consumer projects. This eliminates manual schema maintenance and ensures configuration files remain synchronized with code definitions.

## Glossary

- **Schema_Generator**: The NUKE build target that orchestrates QuickType execution to generate JSON schemas
- **QuickType_Tool**: The .NET CLI tool that converts C# classes to JSON Schema format
- **Build_Pipeline**: The NUKE build system that orchestrates compilation, schema generation, and packaging
- **Schema_File**: The generated build.config.schema.json file containing JSON Schema definitions
- **Package_System**: The NuGet packaging mechanism that includes schema files as content
- **Consumer_Project**: A project that installs UnifyBuild.Nuke and uses build.config.json
- **IDE_Validator**: The IDE or editor that provides autocomplete and validation using the schema
- **Config_File**: The build.config.json file in consumer projects
- **Tool_Manifest**: The .config/dotnet-tools.json file that tracks local .NET tool installations

## Requirements

### Requirement 1: QuickType Tool Installation

**User Story:** As a UnifyBuild maintainer, I want QuickType automatically installed as a local tool, so that schema generation works without manual setup.

#### Acceptance Criteria

1. WHEN the Build_Pipeline executes the schema generation target, THE Build_Pipeline SHALL install QuickType_Tool as a local .NET tool if not already present
2. WHEN QuickType_Tool is installed, THE Build_Pipeline SHALL create or update the Tool_Manifest in the .config directory
3. WHEN QuickType_Tool installation fails due to network issues, THE Build_Pipeline SHALL log a clear error message and fail the build
4. WHEN QuickType_Tool is already installed, THE Build_Pipeline SHALL skip installation and proceed to schema generation

### Requirement 2: Schema Generation from C# Classes

**User Story:** As a UnifyBuild maintainer, I want schemas automatically generated from BuildConfigJson.cs, so that the schema always matches the C# class structure.

#### Acceptance Criteria

1. WHEN the Schema_Generator executes, THE Schema_Generator SHALL parse BuildConfigJson.cs and all referenced nested types
2. WHEN the Schema_Generator completes successfully, THE Schema_Generator SHALL produce a valid JSON Schema file at the specified output path
3. WHEN BuildConfigJson.cs contains syntax errors, THE Schema_Generator SHALL fail with a descriptive error message indicating the file and issue
4. WHEN the Schema_Generator produces output, THE Schema_File SHALL include all public properties from BuildConfigJson and nested types (ProjectGroup, NativeBuildConfig, UnityBuildJsonConfig, UnityPackageMappingConfig)
5. IF the output directory does not exist, THEN THE Schema_Generator SHALL create it before writing the Schema_File

### Requirement 3: Schema Type Mapping Accuracy

**User Story:** As a UnifyBuild maintainer, I want C# types correctly mapped to JSON Schema types, so that validation accurately reflects the configuration structure.

#### Acceptance Criteria

1. WHEN a C# property has type string, THE Schema_File SHALL define that property with JSON Schema type "string"
2. WHEN a C# property has type bool, THE Schema_File SHALL define that property with JSON Schema type "boolean"
3. WHEN a C# property has type int or long, THE Schema_File SHALL define that property with JSON Schema type "integer"
4. WHEN a C# property has type Dictionary<K,V>, THE Schema_File SHALL define that property with JSON Schema type "object" and appropriate additionalProperties
5. WHEN a C# property has type Array or List, THE Schema_File SHALL define that property with JSON Schema type "array"
6. WHEN a C# property is nullable, THE Schema_File SHALL include "null" in the type array for that property

### Requirement 4: Schema Validation

**User Story:** As a UnifyBuild maintainer, I want generated schemas validated for correctness, so that invalid schemas are never packaged.

#### Acceptance Criteria

1. WHEN the Schema_Generator produces a Schema_File, THE Schema_Generator SHALL validate that the file contains valid JSON
2. WHEN the Schema_Generator validates the Schema_File, THE Schema_Generator SHALL verify the root element has type "object"
3. WHEN the Schema_Generator validates the Schema_File, THE Schema_Generator SHALL verify a "properties" definition exists
4. IF the Schema_File is invalid JSON or malformed schema, THEN THE Schema_Generator SHALL fail the build with specific validation errors

### Requirement 5: Build Pipeline Integration

**User Story:** As a UnifyBuild maintainer, I want schema generation integrated into the build pipeline, so that schemas are always generated before packaging.

#### Acceptance Criteria

1. THE Build_Pipeline SHALL execute the Schema_Generator target before the Pack target
2. WHEN the Schema_Generator target fails, THE Build_Pipeline SHALL prevent the Pack target from executing
3. WHEN the Pack target executes, THE Schema_File SHALL exist in the artifacts directory
4. THE Build_Pipeline SHALL complete schema generation within 10 seconds under normal conditions

### Requirement 6: NuGet Package Inclusion

**User Story:** As a UnifyBuild maintainer, I want schemas automatically included in NuGet packages, so that consumers receive the schema without additional steps.

#### Acceptance Criteria

1. WHEN the Package_System creates a NuGet package, THE Package_System SHALL include the Schema_File in the contentFiles directory
2. WHEN the Package_System includes the Schema_File, THE Package_System SHALL configure it to copy to consumer project roots on installation
3. WHEN inspecting the NuGet package contents, THE Schema_File SHALL be present at path "contentFiles/any/any/build.config.schema.json"
4. THE Package_System SHALL mark the Schema_File with CopyToOutputDirectory set to PreserveNewest

### Requirement 7: Consumer Schema Access

**User Story:** As a consumer project developer, I want the schema file automatically available after package installation, so that I can reference it in my config file.

#### Acceptance Criteria

1. WHEN a Consumer_Project installs the UnifyBuild.Nuke package, THE Package_System SHALL copy the Schema_File to the Consumer_Project root directory
2. WHEN the Schema_File is copied to a Consumer_Project, THE Schema_File SHALL be named "build.config.schema.json"
3. WHEN a Consumer_Project updates the UnifyBuild.Nuke package, THE Package_System SHALL update the Schema_File to the new version

### Requirement 8: Schema Reference Resolution

**User Story:** As a consumer project developer, I want to reference the schema in my config file, so that my IDE provides autocomplete and validation.

#### Acceptance Criteria

1. WHEN a Config_File contains a "$schema" property with value "./build.config.schema.json", THE IDE_Validator SHALL resolve the reference to the Schema_File in the project root
2. WHEN the IDE_Validator resolves a schema reference, THE IDE_Validator SHALL load the schema for validation and autocomplete
3. IF the Schema_File does not exist at the referenced path, THEN THE IDE_Validator SHALL indicate the schema cannot be found

### Requirement 9: IDE Autocomplete Support

**User Story:** As a consumer project developer, I want IDE autocomplete when editing build.config.json, so that I can discover available configuration options.

#### Acceptance Criteria

1. WHEN a developer edits a Config_File with a valid schema reference, THE IDE_Validator SHALL provide autocomplete suggestions for property names
2. WHEN a developer types a property name, THE IDE_Validator SHALL suggest valid values based on the schema type definitions
3. WHEN a developer hovers over a property, THE IDE_Validator SHALL display documentation from the schema if available

### Requirement 10: Configuration Validation

**User Story:** As a consumer project developer, I want validation errors when my config is invalid, so that I can fix issues before running the build.

#### Acceptance Criteria

1. WHEN a Config_File contains a property not defined in the Schema_File, THE IDE_Validator SHALL display a validation error
2. WHEN a Config_File contains a property with an incorrect type, THE IDE_Validator SHALL display a validation error indicating the expected type
3. WHEN a Config_File contains a required property that is missing, THE IDE_Validator SHALL display a validation error
4. WHEN a Config_File is valid according to the Schema_File, THE IDE_Validator SHALL display no validation errors

### Requirement 11: Schema Synchronization

**User Story:** As a UnifyBuild maintainer, I want the schema to stay synchronized with C# classes, so that configuration validation remains accurate.

#### Acceptance Criteria

1. WHEN BuildConfigJson.cs is modified, THE Schema_Generator SHALL generate an updated Schema_File reflecting the changes on the next build
2. WHEN a property is added to BuildConfigJson.cs, THE Schema_File SHALL include the new property after regeneration
3. WHEN a property is removed from BuildConfigJson.cs, THE Schema_File SHALL not include the removed property after regeneration
4. WHEN a property type changes in BuildConfigJson.cs, THE Schema_File SHALL reflect the new type after regeneration

### Requirement 12: Error Handling and Diagnostics

**User Story:** As a UnifyBuild maintainer, I want clear error messages when schema generation fails, so that I can quickly diagnose and fix issues.

#### Acceptance Criteria

1. WHEN QuickType_Tool fails to parse the C# source file, THE Schema_Generator SHALL log the QuickType error output with file path and line number
2. WHEN the Schema_Generator encounters an error, THE Build_Pipeline SHALL fail with an actionable error message
3. WHEN QuickType_Tool is not installed and installation fails, THE Schema_Generator SHALL provide manual installation instructions in the error message
4. WHEN the Schema_File cannot be written due to permissions, THE Schema_Generator SHALL log a clear error indicating the path and permission issue

### Requirement 13: Documentation and Examples

**User Story:** As a consumer project developer, I want clear documentation on using the schema, so that I can set up validation in my project.

#### Acceptance Criteria

1. THE Build_Pipeline SHALL provide README documentation explaining how to add the "$schema" reference to Config_File
2. THE Build_Pipeline SHALL provide example Config_File content with the correct schema reference
3. THE Build_Pipeline SHALL document troubleshooting steps for common schema-related issues
4. THE Build_Pipeline SHALL document the schema file location and naming convention
