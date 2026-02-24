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
