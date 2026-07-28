# Releasing

This repository uses a shared `0.x` release line across the public artifacts:

- `UnifyBuild.Nuke`
- `UnifyBuild.Tool`
- `unity/com.unifybuild.editor`

## Bump the Version

Use the helper before tagging a release:

```bash
task release:bump-version VERSION=0.3.3
```

Or, if you prefer npm scripts:

```bash
npm run release:bump-version -- -Version 0.3.3
```

The helper updates:

- `GitVersion.yml` `next-version`
- the `Version` fields in both `.csproj` package files
- `unity/com.unifybuild.editor/package.json`
- `.config/dotnet-tools.json`
- the top heading in the root and Unity package changelogs

## Validate Before Tagging

For unpublished versions, bootstrap the local feed and validate the repo against it:

```bash
task tool:bootstrap-local
dotnet tool restore
dotnet unify-build Compile --plan
```

Recommended checks before tagging:

```bash
dotnet build dotnet/src/UnifyBuild.Nuke/UnifyBuild.Nuke.csproj --configuration Release
dotnet build dotnet/src/UnifyBuild.Tool/UnifyBuild.Tool.csproj --configuration Release
```

## Tag and Push

The release workflow is triggered by `v*` tags:

```bash
git tag v0.3.3
git push origin v0.3.3
```

The workflow publishes the NuGet packages, validates the Unity package metadata, and prepares the OpenUPM onboarding artifacts.

## OpenUPM Onboarding

The canonical onboarding metadata lives in:

- `openupm/com.unifybuild.editor.yml`

That file is the source of truth for submitting the package to the OpenUPM curated list. It is validated in the release workflow and attached to release artifacts so maintainers can reuse it when opening or updating the external onboarding PR.

### First-Time Onboarding

1. Open a PR against `openupm/openupm`.
2. Add `openupm/com.unifybuild.editor.yml` to their `data/packages/` directory, adapting only the fields OpenUPM specifically requires during review.
3. Point reviewers at the package root: `unity/com.unifybuild.editor`.
4. After the package is merged into the curated list, future `v*` tags can be picked up by OpenUPM's automated pipeline.

### Ongoing Releases

After onboarding is complete, the normal repo tag flow becomes the publication signal:

1. Bump versions with the helper.
2. Push `v*` tag.
3. NuGet packages publish from the workflow.
4. OpenUPM observes the same tag and builds the Unity package from the curated package entry.

Until external onboarding is complete, GitHub release assets remain the fallback inspection artifact for the Unity package.
