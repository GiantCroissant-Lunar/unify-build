# Build Configuration Example

This document explains the example `build.config.example.json` file and demonstrates best practices for using the JSON schema with UnifyBuild.

## Schema Reference

```json
{
  "$schema": "./build.config.schema.json"
}
```

**The `$schema` property is the key to enabling IDE autocomplete and validation.**

- **Location**: Must be at the top level of your JSON file (first property recommended)
- **Path**: Use `"./build.config.schema.json"` as a relative path from your config file
- **Effect**: Your IDE will automatically provide autocomplete, validation, and documentation tooltips

The schema file (`build.config.schema.json`) is automatically copied to your project root when you install or update the `UnifyBuild.Nuke` NuGet package.

## Version Configuration

```json
{
  "versionEnv": "GITVERSION_MAJORMINORPATCH",
  "artifactsVersion": "1.0.0"
}
```

- **`versionEnv`**: Environment variable name containing the version string (typically from GitVersion)
- **`artifactsVersion`**: Explicit version string that overrides `versionEnv` if set
- **Usage**: Use `versionEnv` for CI/CD pipelines, `artifactsVersion` for local development

## Project Groups

```json
{
  "projectGroups": {
    "packages": {
      "sourceDir": "src",
      "action": "pack",
      "include": ["MyLibrary", "MyLibrary.Core"],
      "exclude": ["MyLibrary.Tests"]
    }
  }
}
```

Project groups define collections of projects to build, pack, or publish. Each group has:

- **`sourceDir`**: Directory containing the projects (relative to repo root)
- **`action`**: What to do with the projects (`"build"`, `"pack"`, or `"publish"`)
- **`include`**: Array of project names to include (without `.csproj` extension)
- **`exclude`**: Array of project names to exclude (optional)

### Common Patterns

**NuGet Packages**:
```json
"packages": {
  "sourceDir": "src",
  "action": "pack",
  "include": ["MyLibrary"]
}
```

**Executable Applications**:
```json
"executables": {
  "sourceDir": "src/apps",
  "action": "build",
  "include": ["MyApp", "MyTool"]
}
```

**Host Services**:
```json
"hosts": {
  "sourceDir": "src/hosts",
  "action": "publish",
  "include": ["MyWebApi", "MyWorkerService"]
}
```

## Native Build Configuration (Optional)

```json
{
  "nativeBuild": {
    "sourceDir": "native",
    "buildDir": "native/build",
    "cmakeArgs": ["-DCMAKE_BUILD_TYPE=Release"]
  }
}
```

For projects with native C++ components:

- **`sourceDir`**: Directory containing CMakeLists.txt
- **`buildDir`**: Output directory for CMake build artifacts
- **`cmakeArgs`**: Additional arguments to pass to CMake

## Unity Build Configuration (Optional)

```json
{
  "unityBuild": {
    "projectPath": "unity/MyUnityProject",
    "buildTarget": "StandaloneWindows64",
    "outputPath": "unity/builds"
  }
}
```

For Unity projects:

- **`projectPath`**: Path to Unity project directory
- **`buildTarget`**: Unity build target platform
- **`outputPath`**: Where to output built Unity artifacts

## Unity Package Mapping (Optional)

```json
{
  "unityPackageMapping": {
    "com.mycompany.mypackage": "unity/packages/mypackage"
  }
}
```

Maps Unity package identifiers to local development directories for package development workflows.

## Pack Options

```json
{
  "packIncludeSymbols": true,
  "packProperties": {
    "GeneratePackageOnBuild": "false",
    "IncludeSymbols": "true",
    "SymbolPackageFormat": "snupkg"
  }
}
```

- **`packIncludeSymbols`**: Include symbol packages (`.snupkg`) when packing
- **`packProperties`**: Additional MSBuild properties passed to `dotnet pack`

### Common Pack Properties

- `GeneratePackageOnBuild`: Set to `"false"` to prevent automatic packing on build
- `IncludeSymbols`: Include debugging symbols in the package
- `SymbolPackageFormat`: Format for symbol packages (`"snupkg"` is recommended)

## Local NuGet Feed Synchronization

```json
{
  "syncLocalNugetFeed": true,
  "localNugetFeedRoot": "build/_artifacts"
}
```

- **`syncLocalNugetFeed`**: Automatically copy packed artifacts to a local NuGet feed
- **`localNugetFeedRoot`**: Root directory for the local feed

This is useful for testing packages locally before publishing to a remote feed.

## Autocomplete-Friendly Structure

The example demonstrates an autocomplete-friendly structure:

1. **Schema reference first**: Place `$schema` at the top for immediate IDE recognition
2. **Logical grouping**: Related properties are grouped together
3. **Consistent naming**: Use camelCase for all property names
4. **Array syntax**: Use arrays for collections (`include`, `exclude`, `cmakeArgs`)
5. **Object nesting**: Use nested objects for complex configurations

## IDE Features

With the schema reference in place, your IDE provides:

### Autocomplete
Type `"` and your IDE will suggest available property names:
- `artifactsVersion`
- `projectGroups`
- `packIncludeSymbols`
- etc.

### Validation
Invalid properties or types are highlighted in real-time:
- ❌ `"action": "invalid"` → Error: must be "build", "pack", or "publish"
- ❌ `"packIncludeSymbols": "yes"` → Error: must be boolean (true/false)

### Documentation
Hover over any property to see its description and expected type.

## Minimal Example

For a simple project, you only need:

```json
{
  "$schema": "./build.config.schema.json",
  "artifactsVersion": "1.0.0",
  "projectGroups": {
    "packages": {
      "sourceDir": "src",
      "action": "pack",
      "include": ["MyProject"]
    }
  }
}
```

All other properties are optional and can be added as needed.

## Next Steps

1. Copy `build.config.example.json` to your project as `build.config.json`
2. Verify the `$schema` reference points to the correct location
3. Customize the configuration for your project structure
4. Use IDE autocomplete to discover additional properties
5. Run `dotnet unify-build PackProjects` to test your configuration
