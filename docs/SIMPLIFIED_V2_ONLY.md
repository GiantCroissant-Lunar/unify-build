# V2-Only Schema Simplification

**Date**: 2025-12-13
**Decision**: Remove v1 schema support; the remaining schema is now the canonical (unversioned) schema

---

## Changes Made

### 1. Removed V1 Schema

**File**: `src/UnifyBuild.Nuke/BuildConfigJson.cs`
- **Before**: Contained `BuildJsonConfig` (v1), `BuildContextLoaderV1`, `BuildContextLoader` (unified)
- **After**: Only contains comment redirecting to v2 schema

### 2. Simplified Class Names

**File**: `src/UnifyBuild.Nuke/BuildConfigJson.cs`

| Before | After | Reason |
|--------|-------|--------|
| `BuildJsonConfigV2` | `BuildJsonConfig` | Only schema now |
| `BuildContextLoaderV2` | `BuildContextLoader` | Only loader now |
| `LoadV2Config()` | `LoadConfig()` | No versioning needed |

### 3. Direct Schema Requirement

**Behavior Change**:
```csharp
// Before: Auto-detection with fallback to v1
if (json.Contains("projectGroups"))
    return V2Loader.FromJson(...);
else
    return V1Loader.FromJson(...);  // Fallback

// After: Require projectGroups
if (!json.Contains("projectGroups"))
    throw new Exception("Must use projectGroups schema");
return LoadConfig(...);
```

---

## Package Info

**Package**: `UnifyBuild.Nuke.2.0.0.nupkg` ✅
**Build**: Successful
**Breaking Change**: Yes - v1 configs no longer work

---

## Migration Required

All consumers (mung-bean*) must update configs to use projectGroups:

### Before (v1):
```json
{
  "hostsDir": "project/hosts",
  "pluginsDir": "project/plugins",
  "includeHosts": ["MungBean.Console.TerminalGui"]
}
```

### After (v2):
```json
{
  "projectGroups": {
    "executables": {
      "sourceDir": "project/hosts",
      "action": "publish",
      "include": ["MungBean.Console.TerminalGui"]
    },
    "plugins": {
      "sourceDir": "project/plugins",
      "action": "publish"
    }
  }
}
```

---

## File Rename Needed

N/A (file naming is now unversioned).

---

## Next Steps

1. ✅ V1 removed
2. ✅ V2 simplified
3. ✅ Build successful
4. ✅ Use unversioned schema names in repo config (build.config.json + build.config.schema.json)
5. ⏳ Migrate mung-bean* configs to v2 schema
6. ⏳ Test all mung-bean* builds

---

## Benefits of V2-Only

**Simplicity**:
- No dual schema complexity
- Single source of truth
- Cleaner codebase

**Clarity**:
- No version confusion
- No auto-detection needed
- Explicit schema requirement

**Performance**:
- No schema detection overhead
- Faster config loading

**Maintainability**:
- Less code to maintain
- Fewer edge cases
- Simpler testing

---

## Rollout

Since you control all consumers (mung-bean*), can do atomic migration:

1. Publish UnifyBuild.Nuke 2.0.0
2. Update all mung-bean* build configs simultaneously
3. Update all mung-bean* to reference 2.0.0
4. Test builds
5. Commit all changes together

No gradual migration needed - clean cutover.
