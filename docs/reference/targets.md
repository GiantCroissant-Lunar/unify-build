# Targets Reference

Every build capability in UnifyBuild is a NUKE `Target` defined on a [component
interface](../architecture/index.md#component-interface-pattern). This page catalogs all targets,
which interface provides them, and what they depend on.

Run a target by name:

```bash
dotnet unify-build Compile
```

Target names are case-insensitive on the command line.

## Availability

Not every interface is composed into the `unify-build` CLI tool. The tool's `Build` class
implements:

```csharp
class Build : NukeBuild,
    IUnify,                  // IUnifyCompile + IUnifyPack + IUnifyPublish
    IUnifyNative,
    IUnifyUnity,
    IUnifyGodot,
    IUnifySchemaGeneration,
    IUnifyMobile,
    IUnifyUnityExport
```

| Availability | Meaning |
|---|---|
| **CLI** | Runnable via `dotnet unify-build <Target>` out of the box |
| **Library only** | The interface ships in `UnifyBuild.Nuke` but is not composed into the CLI. Implement it in your own NUKE build class to use it |

`IUnifyRust` and `IUnifyGo` are **library only**. To use `RustBuild` or `GoBuild`, add them to
your own build class:

```csharp
class Build : NukeBuild, IUnify, IUnifyRust, IUnifyGo
{
    BuildContext IUnifyBuildConfig.UnifyConfig =>
        BuildContextLoader.FromJson(RootDirectory, "build.config.json");

    public static int Main() => Execute<Build>();
}
```

## Compile — `IUnifyCompile`

| Target | Depends on | Description |
|---|---|---|
| `Compile` | — | Build the solution named by `solution`. No-op when `solution` is not set |
| `CompileProjects` | — | Build each project listed in `compileProjects` |

Both apply `Configuration` (default `Release`) and set the MSBuild `Version` property when a
version is resolved.

## Pack — `IUnifyPack : IUnifyCompile`

| Target | Depends on | Description |
|---|---|---|
| `Pack` | `Compile` | Pack projects into NuGet packages |
| `PackContracts` | `Compile` | Pack all contract projects |
| `PackProjects` | `Pack` | Alias for `Pack` that also triggers `SyncLocalFeed` when configured |
| `PackAll` | `PackContracts`, `Pack` | Pack contracts, pack remaining projects, sync to the local feed |
| `SyncLocalFeed` | `Pack` | Copy produced `.nupkg` files to the local NuGet feed |

`SyncLocalFeed` is gated by `OnlyWhenStatic` on the [`syncLocalNugetFeed`](configuration-reference.md#local-nuget-feed-sync)
setting — it is skipped entirely when that flag is `false`.

!!! note "Feed layout"
    `SyncLocalFeed` writes to the feed root directly, **not** into a `flat/` or `hierarchical/`
    subdirectory. The feed root *is* the flat-layout feed; the legacy subdirectory convention is
    retired. The `localNugetFeedFlatSubdir` and `localNugetFeedHierarchicalSubdir` settings remain
    in the schema for backward compatibility.

## Publish — `IUnifyPublish : IUnifyCompile`

| Target | Depends on | Description |
|---|---|---|
| `PublishHosts` | `Compile` | Publish all host projects |
| `PublishPlugins` | `Compile` | Publish all plugin projects |
| `PublishProjects` | `Compile` | Publish each project listed in `publishProjects` |
| `SyncLatestArtifacts` | `PublishHosts`, `PublishPlugins` | Mirror the effective artifacts version folder to `build/_artifacts/latest` |

## Native — `IUnifyNative`

| Target | Depends on | Description |
|---|---|---|
| `Native` | — | Configure and build native components via CMake |

Configured by [`nativeBuild`](configuration-reference.md#native-build-configuration). Auto-detects
a vcpkg toolchain when `autoDetectVcpkg` is set, preferring `VCPKG_ROOT` over a repo-local
`./vcpkg/`. Artifacts land in `build/_artifacts/{version}/native`.

## Rust — `IUnifyRust` *(library only)*

| Target | Depends on | Description |
|---|---|---|
| `RustBuild` | — | Build Rust crates via Cargo |

Configured by [`rustBuild`](configuration-reference.md#rust-build-configuration). Artifacts land in
`build/_artifacts/{version}/rust`.

## Go — `IUnifyGo` *(library only)*

| Target | Depends on | Description |
|---|---|---|
| `GoBuild` | — | Build Go modules |

Configured by [`goBuild`](configuration-reference.md#go-build-configuration). Artifacts land in
`build/_artifacts/{version}/go`.

## Unity packages — `IUnifyUnity : IUnifyCompile`

| Target | Depends on | Description |
|---|---|---|
| `BuildForUnity` | — | Build `netstandard2.1` DLLs and copy them into Unity packages |

Configured by [`unityBuild`](configuration-reference.md#unity-build-configuration). This target
does **not** produce a Unity player — it only stages managed DLLs into UPM package folders.

## Unity player export — `IUnifyUnityExport`

| Target | Depends on | Description |
|---|---|---|
| `UnityExport` | — | Export the Unity project for all configured platforms |
| `UnityExportDesktop` | — | Export desktop platforms only (Windows, macOS, Linux) |
| `UnityExportMobile` | — | Export mobile platforms as native Gradle/Xcode projects |

Configured by [`unityExport`](configuration-reference.md#unity-export-configuration). These targets
drive the Unity Editor in batch mode via `-executeMethod`, calling into the entrypoint shipped in
the `com.unifybuild.editor` package.

`UnityExportMobile` produces **native projects, not binaries**. Hand the result to
[`MobileBuildAndroidFromProject`](#mobile-ios-android-iunifymobile) or `MobileBuildIosFromProject`
to produce an APK/AAB/IPA.

## Godot export — `IUnifyGodot : IUnifyCompile`

| Target | Depends on | Description |
|---|---|---|
| `BuildGodot` | `Compile` | Export the Godot project for all configured platforms |
| `BuildGodotDesktop` | `Compile` | Export desktop platforms only |
| `BuildGodotMobile` | `Compile` | Export mobile platforms as native Gradle/Xcode projects |

Configured by [`godotBuild`](configuration-reference.md#godot-build-configuration). Artifacts land
in `build/_artifacts/{version}/godot`.

Desktop platforms produce final binaries. As with Unity, `BuildGodotMobile` produces native
projects that the mobile targets then build.

## Mobile (iOS / Android) — `IUnifyMobile`

All mobile targets depend on `MobileRestore`.

| Target | Description |
|---|---|
| `MobileRestore` | Install Fastlane and Ruby dependencies for mobile projects |
| `MobileBuildIos` | Build the iOS app via Fastlane |
| `MobileBuildAndroid` | Build the Android app via Fastlane |
| `MobileBuildIosFromProject` | Build an IPA from an engine-exported Xcode project |
| `MobileBuildAndroidFromProject` | Build an APK/AAB from an engine-exported Gradle project |
| `MobileDeployIosBeta` | Deploy to TestFlight |
| `MobileDeployAndroidBeta` | Deploy to the Play Store internal track |
| `MobileDeployIosRelease` | Deploy to the App Store |
| `MobileDeployAndroidRelease` | Deploy to Play Store production |

Configured by [`mobileBuild`](configuration-reference.md#mobile-build-configuration). Each target
invokes the Fastlane lane named by `buildLane` / `betaLane` / `releaseLane` for that platform.

The `*FromProject` variants are the bridge from engine exports: run `UnityExportMobile` or
`BuildGodotMobile` first, then the matching `*FromProject` target.

!!! warning "Deploy targets publish to real stores"
    `MobileDeploy*` targets upload builds to TestFlight, the Play Store, and the App Store. They
    require valid store credentials in the environment and are not reversible once a build is
    submitted.

## Schema generation — `IUnifySchemaGeneration`

| Target | Runs | Description |
|---|---|---|
| `GenerateSchema` | Before `Pack` | Generate `build.config.schema.json` from `BuildJsonConfig` via NJsonSchema reflection |

Output goes to `build/_artifacts/build.config.schema.json`, and the target is ordered before `Pack`
so the generated schema ships inside the package.

!!! info "Repository-internal target"
    `GenerateSchema` is gated by `OnlyWhenDynamic` on the presence of
    `dotnet/src/UnifyBuild.Nuke/BuildConfigJson.cs`. It is intended for building UnifyBuild itself
    and silently skips in consumer repositories.

## CLI command targets

`Init`, `Validate`, `Doctor`, and `Migrate` are also NUKE targets, defined on the tool's `Build`
class rather than on a shared interface. They are documented in the [CLI Reference](cli.md).

## Dependency graph

```
Compile ──┬─► Pack ──┬─► PackProjects
          │          ├─► PackAll ◄── PackContracts ◄── Compile
          │          └─► SyncLocalFeed        (only when syncLocalNugetFeed)
          │
          ├─► PublishHosts ──┬─► SyncLatestArtifacts
          ├─► PublishPlugins ─┘
          ├─► PublishProjects
          │
          ├─► BuildGodot / BuildGodotDesktop / BuildGodotMobile
          └─► PackContracts

GenerateSchema ──(before)──► Pack

MobileRestore ──► MobileBuild* / MobileDeploy*

(no dependencies)  Native, RustBuild, GoBuild, BuildForUnity,
                   UnityExport, UnityExportDesktop, UnityExportMobile,
                   CompileProjects
```

## Typical sequences

=== "Library → NuGet"

    ```bash
    dotnet unify-build PackAll
    ```

=== "Application → executables"

    ```bash
    dotnet unify-build PublishHosts
    dotnet unify-build SyncLatestArtifacts
    ```

=== "Unity game → Android"

    ```bash
    dotnet unify-build UnityExportMobile
    dotnet unify-build MobileBuildAndroidFromProject
    dotnet unify-build MobileDeployAndroidBeta
    ```

=== "Godot game → desktop"

    ```bash
    dotnet unify-build BuildGodotDesktop
    ```
