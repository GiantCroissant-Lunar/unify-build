# Code Review: V2 Schema Implementation

**Reviewer**: Claude (Code Review Agent)
**Date**: 2025-12-13
**Files Reviewed**:
- `src/UnifyBuild.Nuke/BuildConfigJsonV2.cs`
- `src/UnifyBuild.Nuke/BuildContext.cs`
- `build/build.config.v2.json`

---

## Executive Summary

**Status**: ⚠️ **CRITICAL ISSUES FOUND - BREAKS BACKWARD COMPATIBILITY**

The implementation has **jumped to Phase 3** (v1 removal) instead of implementing **Phase 1** (dual schema support) as specified in RFC-0001. This will immediately break all existing consumers (mung-bean*).

### Risk Level: 🔴 HIGH
- **Breaking Change**: All existing v1 configs will fail
- **No Migration Path**: Consumers cannot gradually migrate
- **Violates RFC**: RFC explicitly requires backward compatibility in Phase 1-2

---

## Critical Issues

### 1. ❌ WRONG CLASS NAMES (Breaking)

**File**: `BuildConfigJsonV2.cs`
**Lines**: 14, 121

```csharp
// ❌ CURRENT (WRONG):
public sealed class BuildJsonConfig        // Line 14
public static class BuildContextLoader     // Line 121

// ✅ SHOULD BE:
public sealed class BuildJsonConfigV2
public static class BuildContextLoaderV2
```

**Impact**:
- Line 14: Naming the class `BuildJsonConfig` **overwrites** the v1 class name
- Line 121: Naming the loader `BuildContextLoader` **replaces** the v1 loader
- **Result**: V1 schema is completely removed, not extended

**Evidence**: Comparing files:
- `BuildConfigJson.cs` (v1): Has `public sealed class BuildJsonConfig` (line 8)
- `BuildConfigJsonV2.cs`: **Also** has `public sealed class BuildJsonConfig` (line 14)
- These cannot coexist in the same namespace!

### 2. ❌ NO V1 SUPPORT (Breaking)

**File**: `BuildConfigJsonV2.cs`
**Lines**: 145-151

```csharp
// Phase 3: v1 schema is no longer supported.
if (!json.Contains("\"projectGroups\"", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "Build config appears to use the legacy v1 schema (missing 'projectGroups'). "
        + "v1 is no longer supported. Migrate to v2 schema...");
}
```

**Problem**: This is **Phase 3 behavior** (v1 removal), not Phase 1 (dual support)!

**RFC Requirement (Phase 1)**:
```csharp
// SHOULD detect and support BOTH schemas
if (json.Contains("\"projectGroups\""))
    return LoadV2Config(...);      // V2 schema
else
    return LoadV1Config(...);       // V1 schema (still works!)
```

**Impact**:
- All existing mung-bean* configs will immediately fail
- No gradual migration path
- Violates RFC-0001 Phase 1-2 requirements

### 3. ❌ MISSING DELEGATION TO V1 LOADER

**File**: `BuildConfigJsonV2.cs`
**Line**: 154

```csharp
// ❌ CURRENT: Throws error for v1
throw new InvalidOperationException("v1 is no longer supported...");

// ✅ SHOULD BE: Delegate to v1 loader
return BuildContextLoaderV1.FromJson(repoRoot, configFile);
```

**Why This Matters**:
- RFC Phase 1 requires: "Add v2 schema **alongside** v1"
- RFC Phase 2: "6-month migration period with deprecation warnings"
- RFC Phase 3: "Remove v1 in v3.0.0" (future major version)

Current implementation skips Phases 1-2 entirely!

---

## Medium Issues

### 4. ⚠️ UNUSED VARIABLES

**File**: `BuildConfigJsonV2.cs`
**Lines**: 185-186, 309

```csharp
var publishProjects = new List<string>(...);  // Line 186
var packProjects = new List<string>(...);     // Line 186

var projectNames = allProjects.Select(...);    // Line 309 - Never used
```

**Impact**: Code smell, potential confusion

**Fix**: Either use these variables or document why they're collected

### 5. ⚠️ HARDCODED DEFAULTS STILL DOMAIN-SPECIFIC

**File**: `BuildConfigJsonV2.cs`
**Lines**: 252-254

```csharp
return (
    repoRoot / "project" / "hosts",    // ❌ Still assumes "hosts" directory
    repoRoot / "project" / "plugins",  // ❌ Still assumes "plugins" directory
    null,
    ...
);
```

**Problem**: Even though v2 schema is generic, defaults still assume MungBean structure

**Suggestion**: Use more generic defaults or make them required:
```csharp
return (
    repoRoot / "src" / "apps",      // Generic
    repoRoot / "src" / "libs",      // Generic
    null,
    ...
);
```

---

## Minor Issues

### 6. ℹ️ INCONSISTENT PROPERTY CASING

**File**: `BuildConfigJsonV2.cs`
**Line**: 144

Uses case-insensitive JSON parsing, which is good, but schema uses PascalCase while example uses camelCase:

```json
// build.config.v2.json (camelCase):
{
  "projectGroups": { ... },
  "packProperties": { ... }
}

// BuildJsonConfig class (PascalCase):
public Dictionary<string, ProjectGroup>? ProjectGroups { get; set; }
public Dictionary<string, string>? PackProperties { get; set; }
```

**Impact**: Low - works due to case-insensitive parsing, but inconsistent

**Suggestion**: Document preferred casing in schema

### 7. ℹ️ MISSING XML DOCS FOR SOME METHODS

**File**: `BuildConfigJsonV2.cs`
**Lines**: 294, 328

```csharp
private static List<string> DiscoverProjectsInGroup(...)  // No XML doc
private static string? GetEnv(...)                        // No XML doc
```

**Impact**: Low - reduces code maintainability

---

## Positive Aspects ✅

### 1. ✅ BuildContext Extended Properly

**File**: `BuildContext.cs`
**Lines**: 41-43

```csharp
public string[] PublishProjects { get; init; } = Array.Empty<string>();
public string[] PackProjects { get; init; } = Array.Empty<string>();
```

**Good**: BuildContext was properly extended to support new properties

### 2. ✅ Backward Compatibility Mapping

**File**: `BuildConfigJsonV2.cs`
**Lines**: 240-292

The `MapProjectGroupsToV1Properties` function provides good mapping logic to convert v2 groups to v1 properties. This allows existing `UnifyBuildBase` targets to work.

### 3. ✅ Project Discovery Logic

**File**: `BuildConfigJsonV2.cs`
**Lines**: 294-326

Solid implementation:
- Recursive project discovery
- Proper filtering (obj/bin exclusion)
- Include/exclude logic
- Case-insensitive matching

### 4. ✅ Comprehensive Example Config

**File**: `build/build.config.v2.json`

Good dogfooding example with:
- Explicit `artifactsVersion`
- ProjectGroups usage
- Pack properties

---

## Build Status

**Current**: ✅ Builds successfully
**Package**: `UnifyBuild.Nuke.0.1.7.nupkg` created

**However**: Package contains **breaking changes** that will fail all v1 consumers!

---

## Impact Assessment

### Immediate Impact if Published

| Consumer | Impact | Severity |
|----------|--------|----------|
| **mung-bean** | ❌ Build fails | 🔴 CRITICAL |
| **mung-bean-console** | ❌ Build fails | 🔴 CRITICAL |
| **mung-bean-window** | ❌ Build fails | 🔴 CRITICAL |
| **mung-bean-content** | ❌ Build fails (if exists) | 🔴 CRITICAL |
| **Any other consumer** | ❌ Build fails | 🔴 CRITICAL |

**Reason**: All existing configs use v1 schema without `projectGroups`, which now throws an exception.

---

## Recommended Fixes

### Fix 1: Rename Classes (REQUIRED)

```diff
- public sealed class BuildJsonConfig
+ public sealed class BuildJsonConfigV2

- public static class BuildContextLoader
+ public static class BuildContextLoaderV2
```

### Fix 2: Restore V1 Support (REQUIRED)

```diff
- // Phase 3: v1 schema is no longer supported.
+ // Phase 1: Support both v1 and v2 schemas
  if (!json.Contains("\"projectGroups\"", StringComparison.OrdinalIgnoreCase))
  {
-     throw new InvalidOperationException(
-         "Build config appears to use the legacy v1 schema...");
+     Serilog.Log.Warning(
+         "Detected legacy v1 build configuration schema. "
+         + "Consider migrating to v2 (see docs/rfcs/rfc-0001-generic-build-schema.md)");
+     return BuildContextLoaderV1.FromJson(repoRoot, configFile);
  }
```

### Fix 3: Update BuildConfigJson.cs (v1 loader)

Rename existing loader to avoid conflict:

```diff
  // BuildConfigJson.cs
- public static class BuildContextLoader
+ public static class BuildContextLoaderV1
```

### Fix 4: Create Unified Entry Point

Add a new static class that provides the public API:

```csharp
// New file: BuildConfigLoader.cs
public static class BuildConfigLoader
{
    public static BuildContext FromJson(AbsolutePath repoRoot, string configFile = "build.config.json")
    {
        var path = FindConfigPath(repoRoot, configFile);
        var json = File.ReadAllText(path);

        // Auto-detect schema version
        if (json.Contains("\"projectGroups\"", StringComparison.OrdinalIgnoreCase))
        {
            Serilog.Log.Information("Detected v2 build configuration schema");
            return BuildContextLoaderV2.FromJson(repoRoot, configFile);
        }
        else
        {
            Serilog.Log.Warning("Detected v1 schema - consider migrating to v2");
            return BuildContextLoaderV1.FromJson(repoRoot, configFile);
        }
    }
}
```

---

## Compliance with RFC-0001

| RFC Requirement | Status | Notes |
|-----------------|--------|-------|
| G1: Enable dogfooding | 🟡 Partial | Schema exists but breaks existing builds |
| G2: Replace domain terminology | ✅ Yes | ProjectGroups are generic |
| G3: Multiple build actions | ✅ Yes | publish/pack/compile supported |
| G4: Backward compatibility | ❌ **FAILED** | v1 support removed! |
| G5: Reusable for non-plugin projects | ✅ Yes | Generic design |
| Phase 1: Dual schema support | ❌ **FAILED** | Only v2 supported |
| Phase 2: Migration period | ❌ **SKIPPED** | Went straight to Phase 3 |
| Success: Zero breaking changes | ❌ **FAILED** | 100% breaking for v1 users |

---

## Conclusion

**Overall Assessment**: ⚠️ **DO NOT PUBLISH AS-IS**

The implementation has excellent design and solid code quality, but **critically violates the RFC's backward compatibility requirements**. Publishing this as `UnifyBuild.Nuke.0.1.7` would be a **breaking change disguised as a patch version**, breaking all downstream consumers.

### Required Actions Before Publish:

1. ✅ Rename `BuildJsonConfig` → `BuildJsonConfigV2`
2. ✅ Rename `BuildContextLoader` → `BuildContextLoaderV2`
3. ✅ Rename existing `BuildContextLoader` → `BuildContextLoaderV1`
4. ✅ Restore v1 schema support with deprecation warnings
5. ✅ Create unified entry point that auto-detects version
6. ✅ Update version to `0.2.0` (pre-1.0 feature milestone)
7. ✅ Test with existing mung-bean* v1 configs
8. ✅ Add deprecation warnings for v1 usage

### Alternative: Publish as v0.2.0-preview

If you want to get v2 schema out for testing:
- Publish as `0.2.0-preview1` (prerelease)
- Document that it's breaking
- Give consumers time to test migration
- Release stable `0.2.0` after validation

---

## Recommendation

**Option 1: Fix Critical Issues (Preferred)**
- Implement fixes 1-4 above
- Keep backward compatibility
- Publish as `UnifyBuild.Nuke 0.2.0` (pre-1.0 feature release)
- RFC Phase 1 complete ✅

**Option 2: Rollback and Restart**
- Revert `BuildConfigJsonV2.cs` changes to class names
- Start fresh following RFC Phase 1 checklist
- More time, but cleaner

**Option 3: Document as Breaking**
- Accept this is v0.4.0 (Phase 3)
- Update RFC timeline (skip Phase 1-2)
- Require all consumers migrate immediately
- High risk, not recommended

---

## Next Steps

1. **Decide**: Which fix option to pursue?
2. **Implement**: Critical fixes (class renames, v1 support restoration)
3. **Test**: Verify v1 configs still work
4. **Update**: Version number and changelog
5. **Document**: Migration guide for v1 → v2
6. **Validate**: Test dogfooding scenario
7. **Publish**: As `0.2.0` with dual support

---

**Sign-off**: Code review complete. Recommend addressing critical issues before merge/publish.
