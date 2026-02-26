# Troubleshooting

This guide covers common errors you may encounter when using UnifyBuild, organized by error code category.

## Error Code Reference

UnifyBuild uses structured error codes prefixed with `UB` for programmatic handling:

| Range | Category | Description |
|-------|----------|-------------|
| UB1xx | Configuration | Config file issues (missing, malformed, invalid references) |
| UB2xx | Build | Build target and compilation failures |
| UB3xx | Tooling | Missing or incompatible tools |
| UB4xx | Schema | JSON Schema generation and validation errors |

---

## Configuration Errors (UB1xx)

### UB100 — Config Not Found

**Error:** `Build config file 'build.config.json' not found.`

**Cause:** UnifyBuild could not locate `build.config.json` in the current directory or any parent directory. It also checks `build/build.config.json`.

**Fix:**
1. Ensure you're running from within a repository that has a `build.config.json`.
2. Generate one with `dotnet unify-build init`.
3. If your config is in a non-standard location, run from the directory containing it.

### UB101 — Config Parse Error

**Error:** JSON parse error with line and column numbers.

**Cause:** The `build.config.json` file contains invalid JSON (missing commas, trailing commas, unquoted keys, etc.).

**Fix:**
1. Check the reported line and column number for syntax issues.
2. Use an editor with JSON validation (VS Code, Rider) to highlight errors.
3. Run `dotnet unify-build validate` to get detailed diagnostics.

### UB102 — Config Schema Violation

**Error:** Invalid property path or unexpected type in config.

**Cause:** A property in `build.config.json` doesn't match the expected schema — wrong type, unknown property name, or missing required field.

**Fix:**
1. Add `"$schema": "./build.config.schema.json"` to your config for IDE autocomplete.
2. Check the [Configuration Reference](./configuration-reference.md) for valid property names and types.
3. Run `dotnet unify-build validate` for specific schema violation details.

### UB103 — Project Not Found

**Error:** Referenced project could not be found at the expected path.

**Cause:** A project name listed in `include` doesn't match any `.csproj` file in the group's `sourceDir`.

**Fix:**
1. Verify the project name matches the `.csproj` filename (without extension). Names are case-insensitive.
2. Check that `sourceDir` points to the correct directory.
3. Run `dotnet unify-build doctor` to verify all project references.

### UB104 — Directory Not Found

**Error:** `Source directory '{path}' does not exist, skipping group`

**Cause:** The `sourceDir` specified in a project group doesn't exist on disk.

**Fix:**
1. Verify the path is relative to the repository root.
2. Create the directory, or update `sourceDir` to the correct path.
3. Use `dotnet unify-build doctor` to check all configured paths.

### UB105 — Duplicate Project

**Error:** The same project appears in multiple groups or is listed more than once.

**Cause:** A `.csproj` file is referenced by multiple project groups, which can cause conflicting build actions.

**Fix:**
1. Use `include`/`exclude` filters to ensure each project appears in only one group.
2. If intentional (e.g., compile in one group, pack in another), ensure the actions are compatible.

---

## Build Errors (UB2xx)

### UB200 — Build Target Failed

**Error:** A NUKE build target failed during execution.

**Cause:** General build target failure. The error details will include the specific target name and underlying error.

**Fix:**
1. Check the build output for the specific error message.
2. Run with `--verbosity verbose` for detailed logging of executed commands.
3. Try building the failing project directly with `dotnet build` to isolate the issue.

### UB201 — Compilation Failed

**Error:** One or more projects failed to compile.

**Cause:** C# compiler errors in your source code.

**Fix:**
1. Check the compiler error output for file paths and error codes.
2. Fix the reported compilation errors in your source files.
3. Ensure all NuGet package references are restored: `dotnet restore`.

### UB202 — Native Build Failed

**Error:** CMake configure or build step failed.

**Cause:** The native (CMake) build encountered an error — missing dependencies, invalid CMake configuration, or C++ compilation errors.

**Fix:**
1. Check the CMake error output included in the build log.
2. Verify CMake is installed: `cmake --version`.
3. If using vcpkg, ensure it's properly configured and `VCPKG_ROOT` is set.
4. Try running CMake manually to isolate the issue:
   ```bash
   cmake -S native -B native/build -DCMAKE_BUILD_TYPE=Release
   cmake --build native/build --config Release
   ```

### UB203 — Unity Build Failed

**Error:** Unity package build failed.

**Cause:** The Unity build step failed — typically a `netstandard2.1` compilation issue or missing Unity project structure.

**Fix:**
1. Verify the `unityProjectRoot` path exists and contains a valid Unity project.
2. Ensure source projects target `netstandard2.1`.
3. Check that all `dependencyDlls` are available in the build output.

---

## Tool Errors (UB3xx)

### UB300 — Tool Not Found

**Error:** A required build tool is not installed or not in PATH.

**Cause:** UnifyBuild couldn't find a required tool (dotnet, CMake, Cargo, Go, etc.).

**Fix:**
1. Install the missing tool and ensure it's in your PATH.
2. Run `dotnet unify-build doctor` to check all tool dependencies.
3. For .NET tools, run `dotnet tool restore`.

### UB301 — Tool Version Mismatch

**Error:** An installed tool version is incompatible.

**Cause:** The installed version of a tool doesn't meet the minimum requirements.

**Fix:**
1. Update the tool to a compatible version.
2. Check the build output for the expected version range.

---

## Schema Errors (UB4xx)

### UB400 — Schema Generation Failed

**Error:** Failed to generate the JSON Schema file.

**Cause:** An error occurred while generating `build.config.schema.json` from the `BuildJsonConfig` class.

**Fix:**
1. This is typically an internal error. Report it with your configuration.
2. As a workaround, manually copy the schema from the NuGet package cache.

### UB401 — Schema Validation Failed

**Error:** Config does not conform to the JSON Schema.

**Cause:** The `build.config.json` file has properties or values that don't match the schema definition.

**Fix:**
1. Run `dotnet unify-build validate` for detailed validation output with line numbers.
2. Check the [Configuration Reference](./configuration-reference.md) for correct property types.
3. Ensure you're using the schema version that matches your UnifyBuild package version.

---

## General Tips

### Enable Verbose Logging

For detailed output including all executed commands:

```bash
dotnet unify-build Compile --verbosity verbose
```

### Run the Doctor

The `doctor` command checks your environment and configuration in one pass:

```bash
dotnet unify-build doctor
```

It verifies:
- NUKE installation and version
- .NET SDK availability
- Config file validity
- All project references exist
- All source directories exist
- No duplicate project references

Use `--fix` to auto-resolve fixable issues:

```bash
dotnet unify-build doctor --fix
```

### Schema File Not Updating

If your IDE isn't picking up schema changes after a package update:

1. Verify the schema file exists: `ls build.config.schema.json`
2. Manually copy from NuGet cache if needed:
   - Windows: `%USERPROFILE%\.nuget\packages\unifybuild.nuke\<version>\contentFiles\any\any\build.config.schema.json`
   - macOS/Linux: `~/.nuget/packages/unifybuild.nuke/<version>/contentFiles/any/any/build.config.schema.json`
3. Restart your IDE to refresh the schema cache.

### Config File Not Found

UnifyBuild searches for `build.config.json` by walking up from the current directory. It checks:

1. `{currentDir}/build.config.json`
2. `{currentDir}/build/build.config.json`
3. Repeats for each parent directory

Make sure you're running commands from within your repository.
