# UnifyBuild.Nuke

`UnifyBuild.Nuke` is the foundation NuGet package for UnifyBuild-powered NUKE builds.

It supports:

- Loading `build.config.json`
- Discovering project groups for compile, pack, and publish flows
- Native, Unity, Godot, mobile, Rust, and Go build contexts
- Shared packaging and artifact conventions

Use `UnifyBuild.Nuke` when you want to compose custom NUKE builds directly.

Use `UnifyBuild.Tool` when you want the prebuilt CLI surface on top of the same component model.

Unity editor entrypoints are packaged separately in `com.unifybuild.editor`, while the .NET side keeps the orchestration targets that call into Unity batch mode.

Repository documentation lives in the root project docs and README.
