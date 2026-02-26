# UnifyBuild VS Code Extension

VS Code extension for the [UnifyBuild](https://github.com/unifybuild/unifybuild) .NET build orchestration system.

## Features

- **IntelliSense** — Enhanced autocomplete and validation for `build.config.json` files via bundled JSON Schema
- **Hover Documentation** — Hover over any property in `build.config.json` to see inline documentation
- **Code Snippets** — Quickly scaffold configs with `unifybuild-config`, `unifybuild-project-group`, and `unifybuild-native-build`
- **Project Groups Tree View** — Browse project groups from `build.config.json` in the Explorer sidebar
- **Command Palette** — Run `init`, `validate`, and `doctor` commands directly from VS Code

## Commands

| Command | Description |
|---------|-------------|
| `UnifyBuild: Init` | Scaffold a new `build.config.json` |
| `UnifyBuild: Validate Config` | Validate the current config against the schema |
| `UnifyBuild: Doctor` | Run diagnostics and check for common issues |

## Snippets

| Prefix | Description |
|--------|-------------|
| `unifybuild-config` | Full `build.config.json` template |
| `unifybuild-project-group` | Project group entry |
| `unifybuild-native-build` | Native CMake build configuration |

## Requirements

- [.NET SDK](https://dotnet.microsoft.com/download) 8.0 or later
- [UnifyBuild Tool](https://www.nuget.org/packages/UnifyBuild.Tool) installed as a dotnet tool

## Development

```bash
cd vscode-extension
npm install
npm run compile
```

Press `F5` in VS Code to launch the Extension Development Host for testing.
