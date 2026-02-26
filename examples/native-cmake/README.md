# Native CMake Build Example

Demonstrates building a C++ library with CMake alongside .NET projects using UnifyBuild.

## Structure

```
native-cmake/
├── build.config.json          # UnifyBuild configuration
├── NativeCmake.sln            # Solution file
├── native/                    # C++ source code
│   ├── CMakeLists.txt         # CMake build definition
│   └── src/
│       ├── mylib.h            # Public header
│       └── mylib.cpp          # Implementation
└── src/
    └── NativeInterop/         # .NET P/Invoke wrapper
        └── NativeWrapper.cs
```

## Configuration Highlights

- `nativeBuild.enabled: true` activates the CMake build pipeline
- `cmakeSourceDir` points to the directory containing `CMakeLists.txt`
- `artifactPatterns` specifies which native build outputs to collect
- The .NET interop project wraps the native library via P/Invoke

## Commands

```bash
# Build .NET and native code
dotnet unify-build Compile

# Build native only
dotnet unify-build Native
```

Native artifacts are collected to `build/_artifacts/{version}/native/`.

## Prerequisites

- CMake 3.16+ installed and in PATH
- A C++ compiler (MSVC on Windows, GCC/Clang on Linux/macOS)

## Learn More

See the [Native Build Example documentation](../../docs/examples/native-build.md) for advanced options including vcpkg integration and CMake presets.
