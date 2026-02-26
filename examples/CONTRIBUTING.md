# Contributing Examples

We welcome community contributions of new example projects.

## Guidelines

1. Create a new directory under `examples/` with a descriptive kebab-case name
2. Include a `build.config.json` demonstrating the configuration pattern
3. Include a `README.md` explaining:
   - What the example demonstrates
   - The project structure
   - Key configuration highlights
   - Commands to build/run the example
4. Keep implementations minimal — just enough to demonstrate the pattern
5. Include a `.sln` file if the example has .NET projects
6. Reference the shared schema: `"$schema": "../../dotnet/samples/consumer-test/build.config.schema.json"`

## Validation

All examples are validated in CI. Before submitting, ensure:

- `dotnet restore` succeeds for .NET examples
- `build.config.json` is valid against the schema
- The README accurately describes the example

## Example Ideas

- Rust + .NET interop (`rustBuild` configuration)
- Go + .NET interop (`goBuild` configuration)
- Blazor WebAssembly application
- gRPC service with shared contracts
- Plugin architecture with dynamic loading
