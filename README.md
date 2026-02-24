 # unify-build
 
Reusable build tooling (NUKE) used across GiantCroissant-Lunar repos.

## Usage (recommended)

This repo ships a local `dotnet tool` (`UnifyBuild.Tool`) that runs build targets based on `build.config.json`.

From the repo root:

```powershell
dotnet tool restore
dotnet unify-build PackProjects --configuration Release
```

Targets are NUKE targets (e.g. `Compile`, `PackProjects`, `PublishHosts`, `PublishPlugins`, `SyncLatestArtifacts`).

## UnifyBuild.Nuke

`UnifyBuild.Nuke` is a small library consumed by NUKE build scripts to:
 
 - Locate and parse build configuration JSON
 - Discover `.csproj` files from configured directories
 - Provide a unified `BuildContext` used by build targets
 
### Build config schema
 
 Create a config file such as `build/build.config.json`:
 
 ```json
 {
   "artifactsVersion": "local",
   "projectGroups": {
     "packages": {
       "sourceDir": "src",
       "action": "pack",
       "include": ["UnifyBuild.Nuke"]
     }
   }
 }
 ```
 
 `projectGroups` is required.
 
 ### Loading the config in a NUKE build
 
 ```csharp
 var ctx = BuildContextLoader.FromJson(RepoRoot, "build.config.json");
 ```

## JSON Schema Support

The `UnifyBuild.Nuke` package includes a JSON schema file (`build.config.schema.json`) that enables IDE autocomplete and validation for your `build.config.json` files.

### Adding Schema Reference

Add a `$schema` property at the top of your `build.config.json`:

```json
{
  "$schema": "./build.config.schema.json",
  "artifactsVersion": "local",
  "projectGroups": {
    "packages": {
      "sourceDir": "src",
      "action": "pack",
      "include": ["MyProject"]
    }
  }
}
```

The schema file is automatically copied to your project root when you install or update the `UnifyBuild.Nuke` package.

**For a comprehensive example**, see [`build/build.config.example.json`](build/build.config.example.json) and the accompanying documentation in [`build/build.config.example.md`](build/build.config.example.md).

### Schema File Location

- **File name**: `build.config.schema.json`
- **Location**: Project root directory (same directory as your `build.config.json`)
- **Path reference**: Use `"./build.config.schema.json"` as the relative path in your `$schema` property

### IDE Support

Once the schema reference is added, most modern IDEs and editors will provide:

- **Autocomplete**: Property name suggestions as you type
- **Validation**: Real-time error highlighting for invalid properties or types
- **Documentation**: Hover tooltips showing property descriptions
- **Type checking**: Warnings when values don't match expected types

Supported editors include VS Code, Visual Studio, JetBrains Rider, and any editor with JSON Schema support.

### Schema Updates

The schema file is automatically updated when you update the `UnifyBuild.Nuke` package:

1. Run `dotnet add package UnifyBuild.Nuke` (or update via NuGet Package Manager)
2. The new schema file will replace the existing one in your project root
3. Your IDE will automatically reload the updated schema

No manual steps are required to keep the schema synchronized with the package version.

### Troubleshooting

**Schema file not found after package installation**

If the schema file is not copied to your project root:
- Verify the package installed successfully: `dotnet list package | grep UnifyBuild.Nuke`
- Manually copy the schema from the NuGet cache:
  - Windows: `%USERPROFILE%\.nuget\packages\unifybuild.nuke\<version>\contentFiles\any\any\build.config.schema.json`
  - macOS/Linux: `~/.nuget/packages/unifybuild.nuke/<version>/contentFiles/any/any/build.config.schema.json`
- Ensure your project uses SDK-style project format (required for contentFiles support)

**IDE not providing autocomplete**

If your IDE doesn't show autocomplete suggestions:
- Verify the `$schema` property is at the top level of your JSON file
- Check that the schema file exists at the referenced path: `./build.config.schema.json`
- Restart your IDE to refresh the schema cache
- Ensure your IDE has JSON Schema support enabled (usually enabled by default)

**Schema validation errors for valid configuration**

If you see validation errors for a configuration that works correctly:
- Verify you're using the latest version of `UnifyBuild.Nuke`
- Check if the schema file matches your package version
- Report the issue with your configuration example so the schema can be updated

**Custom properties not recognized**

The schema only includes properties defined in the `BuildConfigJson` class. If you need additional properties:
- Check if the property is supported in the current version
- Refer to the package documentation for available configuration options
- Custom properties outside the schema will show validation warnings but won't prevent the build from running
