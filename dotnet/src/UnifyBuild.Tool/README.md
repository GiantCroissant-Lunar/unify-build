# UnifyBuild.Tool

`UnifyBuild.Tool` is the prebuilt CLI surface for UnifyBuild.

It composes the `UnifyBuild.Nuke` foundation package into the `dotnet unify-build` command so consumer repositories can run shared targets without maintaining their own NUKE bootstrap project.

Use this package when you want:

- `dotnet tool restore` + `dotnet unify-build <Target>` workflows
- config-driven compile, pack, publish, and validation commands
- Unity, native, and other orchestration targets from a stable CLI

Use `UnifyBuild.Nuke` instead when you want to author a custom NUKE build directly against the exported component interfaces.
