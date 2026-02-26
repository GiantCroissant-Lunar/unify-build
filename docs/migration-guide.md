# Migration Guide: v1 → v2 Schema

This guide covers migrating your `build.config.json` from the v1 schema to the v2 schema introduced in UnifyBuild 3.0.

## What Changed

The v2 schema replaces domain-specific directory properties with a generic `projectGroups` system:

| v1 Property | v2 Equivalent |
|---|---|
| `hostsDir` | `projectGroups.executables` (action: `publish`) |
| `pluginsDir` | `projectGroups.libraries` (action: `pack`) |
| `contractsDir` | `projectGroups.contracts` (action: `pack`) |
| `includeHosts` / `excludeHosts` | `projectGroups.executables.include` / `.exclude` |
| `includePlugins` / `excludePlugins` | `projectGroups.libraries.include` / `.exclude` |
| `includeContracts` / `excludeContracts` | `projectGroups.contracts.include` / `.exclude` |

The flat arrays `compileProjects`, `publishProjects`, and `packProjects` still work but are considered legacy. Prefer `projectGroups` for new configs.

## Automated Migration

The fastest way to migrate is the built-in command:

```bash
dotnet unify-build migrate
```

This will:

1. Detect your config version automatically
2. Create a backup at `build.config.json.bak`
3. Transform v1 properties into `projectGroups`
4. Validate the migrated config

If your config is already v2, the command exits with no changes.

## Manual Migration

### Before (v1)

```json
{
  "solution": "src/MySolution.sln",
  "version": "1.0.0",
  "hostsDir": "src/hosts",
  "includeHosts": ["MyApp.Api", "MyApp.Worker"],
  "excludeHosts": ["MyApp.DevTool"],
  "pluginsDir": "src/plugins",
  "contractsDir": "src/contracts",
  "nuGetOutputDir": "build/_artifacts/nuget",
  "packIncludeSymbols": true
}
```

### After (v2)

```json
{
  "$schema": "./build.config.schema.json",
  "solution": "src/MySolution.sln",
  "version": "1.0.0",
  "projectGroups": {
    "executables": {
      "sourceDir": "src/hosts",
      "action": "publish",
      "include": ["MyApp.Api", "MyApp.Worker"],
      "exclude": ["MyApp.DevTool"]
    },
    "libraries": {
      "sourceDir": "src/plugins",
      "action": "pack"
    },
    "contracts": {
      "sourceDir": "src/contracts",
      "action": "pack"
    }
  },
  "nuGetOutputDir": "build/_artifacts/nuget",
  "packIncludeSymbols": true
}
```

### Step-by-Step

1. Add a `$schema` reference for IDE IntelliSense:
   ```json
   "$schema": "./build.config.schema.json"
   ```

2. Create a `projectGroups` object and map each v1 directory:
    - `hostsDir` → a group with `action: "publish"`
    - `pluginsDir` → a group with `action: "pack"`
    - `contractsDir` → a group with `action: "pack"`

3. Move `include*` / `exclude*` arrays into the corresponding group's `include` / `exclude` properties.

4. Remove the old v1 properties (`hostsDir`, `pluginsDir`, `contractsDir`, `includeHosts`, etc.).

5. Run validation to confirm:
   ```bash
   dotnet unify-build validate
   ```

## Backward Compatibility

Deprecated v1 properties still work in the current version but produce warnings:

```
[DEPRECATED] Property 'hostsDir' is deprecated. Use projectGroups with action 'publish' instead.
Run 'dotnet unify-build migrate' to auto-migrate.
```

These properties will be removed in the next major version. Migrate now to avoid breakage.

## Common Issues

### "Config is already at v2 schema"

Your config already uses `projectGroups`. No migration needed.

### Migration didn't pick up my custom properties

The migrate command preserves `nativeBuild`, `unityBuild`, `rustBuild`, `goBuild`, and all standard properties. Custom or unknown properties may be dropped. Check the backup file and re-add them manually if needed.

### Validation fails after migration

Run `dotnet unify-build doctor` to diagnose. Common causes:

- Source directories referenced in `projectGroups` don't exist on disk
- Project names in `include`/`exclude` don't match actual `.csproj` file names
- Duplicate project references across groups

### I use `compileProjects` / `publishProjects` / `packProjects`

These flat arrays are still supported alongside `projectGroups`. However, `projectGroups` is the recommended approach for new configurations. You can use both simultaneously — projects from flat arrays and project groups are merged during build.
