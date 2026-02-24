# Test Consumer Project

This is a minimal test project that verifies the UnifyBuild.Nuke package correctly deploys the `build.config.schema.json` file to consumer projects.

## Purpose

This test project validates Requirements 7.1, 7.2, and 7.3:

- **Requirement 7.1**: When a Consumer_Project installs the UnifyBuild.Nuke package, the schema file is copied to the project root directory
- **Requirement 7.2**: The schema file is named "build.config.schema.json"
- **Requirement 7.3**: When a Consumer_Project updates the package, the schema file is updated to the new version

## Test Approach

The test project:

1. References the local UnifyBuild.Nuke package from the artifacts directory
2. Runs integration tests that verify the schema file exists in the project root
3. Validates the schema file structure and content
4. Tests that the schema can be referenced by a build.config.json file

## Running the Tests

### Prerequisites

1. Build the UnifyBuild.Nuke package to generate the local package:
   ```bash
   cd build/nuke
   ./build.ps1 Pack
   ```

2. Ensure the package exists in `build/_artifacts/local/flat/`

### Run Tests

From the sample project directory:

```bash
cd dotnet/samples/consumer-test
dotnet restore
dotnet test
```

Or from the repository root:

```bash
dotnet test dotnet/samples/consumer-test/TestConsumer.csproj
```

## Expected Results

All tests should pass, confirming:

- ✅ Schema file exists in project root
- ✅ Schema file is named correctly
- ✅ Schema file contains valid JSON
- ✅ Schema file has valid JSON Schema structure
- ✅ Schema file includes expected UnifyBuild properties
- ✅ Schema file includes nested type definitions
- ✅ Schema file can be referenced by build.config.json

## Testing Package Updates

To test Requirement 7.3 (schema updates when package is updated):

1. Install version 3.0.0 of the package
2. Verify the schema file exists
3. Build a new version of the package (e.g., 3.0.1)
4. Update the package reference in TestConsumer.csproj
5. Run `dotnet restore` to update the package
6. Verify the schema file is updated with the new version

## Troubleshooting

### Schema file not found

If the schema file is not found in the project root:

1. Check that the package was built with the GenerateSchema target
2. Verify the schema file exists in the package: `unzip -l UnifyBuild.Nuke.*.nupkg | grep schema`
3. Check the package path configuration in UnifyBuild.Nuke.csproj
4. Try cleaning and restoring: `dotnet clean && dotnet restore`

### Package not found

If the package cannot be restored:

1. Verify the package exists in `build/_artifacts/local/flat/`
2. Check the RestoreAdditionalProjectSources path in TestConsumer.csproj
3. Try clearing the NuGet cache: `dotnet nuget locals all --clear`

## Notes

- This is an integration test project, not a unit test project
- The tests verify actual package installation behavior
- The project uses xUnit as the test framework
- The schema file should be automatically copied by NuGet's contentFiles mechanism
