# Monorepo Example

Demonstrates managing multiple services and shared libraries in a single repository using UnifyBuild.

## Structure

```
monorepo/
├── build.config.json          # UnifyBuild configuration
├── Monorepo.sln               # Solution file
├── libs/                      # Shared libraries (packed as NuGet)
│   ├── Common/
│   │   └── Result.cs
│   └── Logging/
│       └── Logger.cs
└── services/                  # Deployable services (published)
    ├── OrderService/
    │   └── Program.cs
    └── InventoryService/
        └── Program.cs
```

## Configuration Highlights

- Two project groups with different actions: `pack` for libraries, `publish` for services
- `sourceDir` separates library and service discovery into distinct directories
- Shared libraries are packed as NuGet packages for internal consumption
- Services are published as deployable executables

## Commands

```bash
# Compile everything
dotnet unify-build Compile

# Pack shared libraries
dotnet unify-build PackProjects --configuration Release

# Publish services
dotnet unify-build PublishHosts --configuration Release
```

## When to Use This Pattern

- Multiple microservices sharing common code
- Internal NuGet packages consumed by multiple services
- Teams working on different services within the same repository
