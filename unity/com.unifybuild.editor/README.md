# com.unifybuild.editor

`com.unifybuild.editor` contains the Unity editor-side entrypoints used by UnifyBuild batch-mode automation.

## What This Package Provides

- `UnifyBuild.Editor.BuildScript.Build` and convenience wrappers for desktop and mobile targets
- a stable `-executeMethod` surface for Unity batch-mode builds
- the Unity-side bridge used by `dotnet unify-build` export and packaging flows

## Relationship to the NuGet Artifacts

- `UnifyBuild.Nuke` is the reusable .NET foundation package
- `UnifyBuild.Tool` is the CLI entrypoint published to NuGet
- `com.unifybuild.editor` is the Unity package distributed separately for UPM/OpenUPM-style consumption

This separation keeps Unity editor assets out of the NuGet packages while preserving Unity orchestration targets on the .NET side.

## Versioning

This package follows the repository release line. The `package.json` version is expected to match the repo release tag and the NuGet package version used for the same release.

## Current Source Location

The canonical source for this package lives in `unity/com.unifybuild.editor` inside the main repository.

Until the OpenUPM publication path is finalized, treat this folder as the source of truth for local or Git-based UPM consumption.
