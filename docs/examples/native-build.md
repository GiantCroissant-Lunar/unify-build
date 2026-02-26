# Example: Native Build (CMake Integration)

Build a C++ library with CMake alongside .NET projects using UnifyBuild.

## Project Structure

```
my-native-project/
├── build.config.json
├── build.config.schema.json
├── src/
│   └── MyApp.Interop/
│       ├── MyApp.Interop.csproj
│       └── NativeWrapper.cs
├── native/
│   ├── CMakeLists.txt
│   ├── src/
│   │   ├── mylib.cpp
│   │   └── mylib.h
│   └── vcpkg.json          # optional vcpkg manifest
├── vcpkg/                   # optional vcpkg submodule
│   └── scripts/buildsystems/vcpkg.cmake
└── MyNativeProject.sln
```

## Configuration

```json
{
  "$schema": "./build.config.schema.json",
  "solution": "MyNativeProject.sln",
  "projectGroups": {
    "interop": {
      "sourceDir": "src",
      "action": "pack",
      "include": ["MyApp.Interop"]
    }
  },
  "nativeBuild": {
    "enabled": true,
    "cmakeSourceDir": "native",
    "buildConfig": "Release",
    "autoDetectVcpkg": true,
    "artifactPatterns": ["*.dll", "*.so", "*.dylib"]
  }
}
```

Key settings:

- **cmakeSourceDir** — directory containing `CMakeLists.txt`
- **buildConfig** — passed to CMake as `--config` (typically `Release` or `Debug`)
- **autoDetectVcpkg** — when `true`, UnifyBuild looks for `vcpkg/scripts/buildsystems/vcpkg.cmake` and passes it as the CMake toolchain file automatically
- **artifactPatterns** — glob patterns for files to collect from the build output into the artifacts directory

## Commands

### Compile .NET and native code

```bash
dotnet unify-build Compile
```

The `Compile` target builds .NET projects first, then runs the native build.

### Build native only

```bash
dotnet unify-build Native
```

### Expected output

```
═══════════════════════════════════════
Target: Native
═══════════════════════════════════════
  Detected vcpkg toolchain: vcpkg/scripts/buildsystems/vcpkg.cmake
  CMake configure: cmake -S native -B native/build -DCMAKE_BUILD_TYPE=Release -DCMAKE_TOOLCHAIN_FILE=vcpkg/scripts/buildsystems/vcpkg.cmake
  CMake build: cmake --build native/build --config Release
  Collecting artifacts matching: *.dll, *.so, *.dylib
  Copied 2 artifact(s) → build/_artifacts/1.0.0/native/
  ✓ Native completed
```

## Advanced Options

### Custom CMake flags

```json
{
  "nativeBuild": {
    "enabled": true,
    "cmakeSourceDir": "native",
    "cmakeOptions": ["-DBUILD_SHARED_LIBS=ON", "-DENABLE_TESTS=OFF"],
    "buildConfig": "Release"
  }
}
```

### CMake presets

If your project uses `CMakePresets.json`, specify the preset name:

```json
{
  "nativeBuild": {
    "enabled": true,
    "cmakeSourceDir": "native",
    "cmakePreset": "release-linux",
    "buildConfig": "Release"
  }
}
```

### Without vcpkg

Set `autoDetectVcpkg: false` (or omit it) if you manage dependencies another way:

```json
{
  "nativeBuild": {
    "enabled": true,
    "cmakeSourceDir": "native",
    "autoDetectVcpkg": false,
    "buildConfig": "Release"
  }
}
```

## Troubleshooting

- **CMake not found** — Install CMake and ensure it's in your PATH. Run `cmake --version` to verify.
- **vcpkg not detected** — UnifyBuild looks for `vcpkg/scripts/buildsystems/vcpkg.cmake` relative to the repo root. Set `VCPKG_ROOT` or place vcpkg as a submodule.
- **Build errors** — Run with `--verbosity verbose` to see the full CMake commands. Try running them manually to isolate the issue. See [Troubleshooting](../troubleshooting.md#ub202--native-build-failed) for more details.
