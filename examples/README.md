# UnifyBuild Community Examples

A collection of example projects demonstrating common UnifyBuild configuration patterns.

## Examples

| Example | Description | Key Features |
|---------|-------------|--------------|
| [dotnet-library](./dotnet-library/) | .NET class library packed as NuGet | `pack` action, symbol packages |
| [dotnet-application](./dotnet-application/) | .NET console app with publish | `publish` action, multiple project groups |
| [native-cmake](./native-cmake/) | CMake C++ build with .NET interop | `nativeBuild`, CMake integration |
| [unity-project](./unity-project/) | Unity package with .NET libraries | `unityBuild`, netstandard2.1 |
| [monorepo](./monorepo/) | Multi-service monorepo | Multiple project groups, mixed actions |
| [multi-target](./multi-target/) | Multi-target framework library | Multiple TFMs, conditional compilation |

## Getting Started

Each example contains:

- `build.config.json` — UnifyBuild configuration for the example
- `README.md` — Explanation of the pattern and how to use it
- Minimal project files demonstrating the configuration

To try an example:

```bash
cd examples/<example-name>
dotnet restore
dotnet unify-build Compile
```

## Contributing

See [CONTRIBUTING.md](./CONTRIBUTING.md) for guidelines on adding new examples.
