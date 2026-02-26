# Extending UnifyBuild

This guide covers how to extend UnifyBuild with custom component interfaces, config properties, and tests. For general contribution guidelines (PR process, commit conventions, code style), see the root [CONTRIBUTING.md](../CONTRIBUTING.md).

## Creating a Custom Component Interface

Every build capability in UnifyBuild is a C# interface that extends `IUnifyBuildConfig`. Here's how to create one.

### Step 1: Define the Interface

Create a new file in `dotnet/src/UnifyBuild.Nuke/` (or your own project referencing `UnifyBuild.Nuke`):

```csharp
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;

namespace UnifyBuild.Nuke;

public interface IUnifyDocker : IUnifyBuildConfig
{
    Target DockerBuild => _ => _
        .Description("Build Docker images")
        .Executes(() =>
        {
            var config = UnifyConfig.DockerBuild;
            if (config is null)
            {
                Serilog.Log.Information("No Docker build configuration found. Skipping.");
                return;
            }

            if (!config.Enabled)
            {
                Serilog.Log.Information("Docker build is disabled. Skipping.");
                return;
            }

            // Detect tool availability
            if (!DetectDocker())
            {
                Serilog.Log.Error("docker not found in PATH. Install Docker: https://docs.docker.com/get-docker/");
                return;
            }

            // Execute build
            var args = $"build -t {config.ImageName}:{config.Tag}";
            if (!string.IsNullOrEmpty(config.Dockerfile))
                args += $" -f \"{config.Dockerfile}\"";
            args += $" \"{config.ContextDir}\"";

            ProcessTasks.StartProcess("docker", args, config.ContextDir)
                .AssertZeroExitCode();
        });

    private static bool DetectDocker()
    {
        try
        {
            var process = ProcessTasks.StartProcess("docker", "--version", logOutput: false);
            process.AssertZeroExitCode();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

Key patterns to follow:
- Extend `IUnifyBuildConfig` to get access to `UnifyConfig`
- Define targets as `Target PropertyName => _ => _` with `.Description()` and `.Executes()`
- Check for null/disabled config early and return gracefully
- Detect external tools before using them
- Use `Serilog.Log` for logging
- Use `ProcessTasks.StartProcess()` for external process execution

### Step 2: Add Config Classes

Add a deserialization model in `BuildConfigJson.cs` (or your own file):

```csharp
public sealed class DockerBuildConfig
{
    public bool Enabled { get; set; } = true;
    public string? ContextDir { get; set; }
    public string? Dockerfile { get; set; }
    public string ImageName { get; set; } = "myapp";
    public string Tag { get; set; } = "latest";
    public Dictionary<string, string>? BuildArgs { get; set; }
}
```

Add a runtime context record:

```csharp
public sealed record DockerBuildContext
{
    public bool Enabled { get; init; } = true;
    public AbsolutePath? ContextDir { get; init; }
    public string? Dockerfile { get; init; }
    public string ImageName { get; init; } = "myapp";
    public string Tag { get; init; } = "latest";
    public Dictionary<string, string> BuildArgs { get; init; } = new();
}
```

The config class uses `string?` for paths (JSON-friendly). The context record uses `AbsolutePath` (NUKE type) for resolved paths.

### Step 3: Wire Into BuildContext

Add the property to `BuildContext`:

```csharp
public sealed record BuildContext
{
    // ... existing properties ...
    public DockerBuildContext? DockerBuild { get; init; }
}
```

Add the property to `BuildJsonConfig`:

```csharp
public sealed class BuildJsonConfig
{
    // ... existing properties ...
    public DockerBuildConfig? DockerBuild { get; set; }
}
```

### Step 4: Add Loader Mapping

In `BuildContextLoader`, add a mapping method and call it from `LoadConfig()`:

```csharp
private static DockerBuildContext? CreateDockerBuildContext(
    AbsolutePath repoRoot, DockerBuildConfig? cfg)
{
    if (cfg is null) return null;

    return new DockerBuildContext
    {
        Enabled = cfg.Enabled,
        ContextDir = string.IsNullOrEmpty(cfg.ContextDir)
            ? repoRoot
            : repoRoot / cfg.ContextDir,
        Dockerfile = cfg.Dockerfile,
        ImageName = cfg.ImageName,
        Tag = cfg.Tag,
        BuildArgs = cfg.BuildArgs ?? new()
    };
}
```

Then in `LoadConfig()`:

```csharp
DockerBuild = CreateDockerBuildContext(repoRoot, config.DockerBuild),
```

### Step 5: Compose in Build.cs

Add the interface to the `Build` class:

```csharp
class Build : NukeBuild, IUnify, IUnifyNative, IUnifyUnity, IUnifyDocker
{
    // ...
}
```

NUKE automatically discovers the `DockerBuild` target and makes it available via `dotnet unify-build DockerBuild`.

### Step 6: Update JSON Schema

Add the new config section to `build.config.schema.json`:

```json
{
  "properties": {
    "dockerBuild": {
      "type": "object",
      "properties": {
        "enabled": { "type": "boolean", "default": true },
        "contextDir": { "type": "string" },
        "dockerfile": { "type": "string" },
        "imageName": { "type": "string" },
        "tag": { "type": "string" },
        "buildArgs": {
          "type": "object",
          "additionalProperties": { "type": "string" }
        }
      }
    }
  }
}
```

## Example: Terraform Component

Here's a more complete example for infrastructure-as-code:

```csharp
public interface IUnifyTerraform : IUnifyBuildConfig
{
    Target TerraformInit => _ => _
        .Description("Initialize Terraform working directory")
        .Executes(() =>
        {
            var config = UnifyConfig.TerraformBuild;
            if (config is null) return;

            ProcessTasks.StartProcess("terraform", "init", config.WorkingDir)
                .AssertZeroExitCode();
        });

    Target TerraformPlan => _ => _
        .Description("Generate Terraform execution plan")
        .DependsOn<IUnifyTerraform>(x => x.TerraformInit)
        .Executes(() =>
        {
            var config = UnifyConfig.TerraformBuild;
            if (config is null) return;

            var args = "plan";
            foreach (var (key, value) in config.Variables)
                args += $" -var \"{key}={value}\"";
            if (!string.IsNullOrEmpty(config.PlanFile))
                args += $" -out=\"{config.PlanFile}\"";

            ProcessTasks.StartProcess("terraform", args, config.WorkingDir)
                .AssertZeroExitCode();
        });

    Target TerraformApply => _ => _
        .Description("Apply Terraform changes")
        .DependsOn<IUnifyTerraform>(x => x.TerraformPlan)
        .Executes(() =>
        {
            var config = UnifyConfig.TerraformBuild;
            if (config is null) return;

            var args = !string.IsNullOrEmpty(config.PlanFile)
                ? $"apply \"{config.PlanFile}\""
                : "apply -auto-approve";

            ProcessTasks.StartProcess("terraform", args, config.WorkingDir)
                .AssertZeroExitCode();
        });
}
```

With corresponding config:

```csharp
public sealed record TerraformBuildContext
{
    public AbsolutePath? WorkingDir { get; init; }
    public Dictionary<string, string> Variables { get; init; } = new();
    public string? PlanFile { get; init; }
}
```

And `build.config.json` usage:

```json
{
  "$schema": "./build.config.schema.json",
  "projectGroups": { },
  "terraformBuild": {
    "workingDir": "infra/terraform",
    "variables": {
      "environment": "staging",
      "region": "us-east-1"
    },
    "planFile": "tfplan"
  }
}
```

## Adding Custom Build_Config Properties

To add a new top-level config section:

1. **Create a config class** (deserialization model with `string?` paths):
   ```csharp
   public sealed class MyFeatureConfig
   {
       public bool Enabled { get; set; } = true;
       public string? SomeDir { get; set; }
   }
   ```

2. **Create a context record** (runtime model with `AbsolutePath`):
   ```csharp
   public sealed record MyFeatureContext
   {
       public bool Enabled { get; init; } = true;
       public AbsolutePath? SomeDir { get; init; }
   }
   ```

3. **Add to `BuildJsonConfig`**:
   ```csharp
   public MyFeatureConfig? MyFeature { get; set; }
   ```

4. **Add to `BuildContext`**:
   ```csharp
   public MyFeatureContext? MyFeature { get; init; }
   ```

5. **Add mapping in `BuildContextLoader.LoadConfig()`**:
   ```csharp
   MyFeature = CreateMyFeatureContext(repoRoot, config.MyFeature),
   ```

6. **Update `build.config.schema.json`** with the new section definition.

All new properties must be optional (nullable) to maintain backward compatibility with existing configs.

## Testing Custom Components

### Unit Tests

Use xUnit to test your component logic in isolation:

```csharp
public class DockerBuildTests
{
    [Fact]
    public void DockerBuildConfig_DefaultValues()
    {
        var config = new DockerBuildConfig();
        Assert.True(config.Enabled);
        Assert.Equal("myapp", config.ImageName);
        Assert.Equal("latest", config.Tag);
    }

    [Fact]
    public void CreateDockerBuildContext_ResolvesAbsolutePaths()
    {
        var repoRoot = (AbsolutePath)"/repo";
        var config = new DockerBuildConfig { ContextDir = "src/app" };

        var context = CreateDockerBuildContext(repoRoot, config);

        Assert.Equal(repoRoot / "src" / "app", context!.ContextDir);
    }

    [Fact]
    public void CreateDockerBuildContext_NullConfig_ReturnsNull()
    {
        var result = CreateDockerBuildContext(
            (AbsolutePath)"/repo", null);
        Assert.Null(result);
    }
}
```

### Config Parsing Tests

Verify your config deserializes correctly from JSON:

```csharp
[Fact]
public void BuildJsonConfig_DeserializesDockerBuild()
{
    var json = """
    {
        "dockerBuild": {
            "enabled": true,
            "contextDir": "src/app",
            "imageName": "myservice",
            "tag": "v1.0"
        }
    }
    """;

    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var config = JsonSerializer.Deserialize<BuildJsonConfig>(json, options);

    Assert.NotNull(config?.DockerBuild);
    Assert.Equal("myservice", config.DockerBuild.ImageName);
}
```

### Integration Tests

Test the full workflow using temporary directories:

```csharp
public class DockerIntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public DockerIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"unify-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void DockerBuild_WithValidConfig_ProducesContext()
    {
        var configJson = """
        {
            "dockerBuild": {
                "contextDir": "app",
                "imageName": "test"
            }
        }
        """;

        File.WriteAllText(Path.Combine(_tempDir, "build.config.json"), configJson);
        Directory.CreateDirectory(Path.Combine(_tempDir, "app"));

        var context = BuildContextLoader.FromJson(
            (AbsolutePath)_tempDir, "build.config.json");

        Assert.NotNull(context.DockerBuild);
        Assert.Equal("test", context.DockerBuild.ImageName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
```

### Property-Based Tests

Use FsCheck to verify properties hold across random inputs:

```csharp
[Property(MaxTest = 100)]
public Property DockerConfig_RoundTrip_PreservesValues()
{
    return Prop.ForAll(
        Arb.From<NonEmptyString>(),
        Arb.From<NonEmptyString>(),
        (imageName, tag) =>
        {
            var config = new DockerBuildConfig
            {
                ImageName = imageName.Get,
                Tag = tag.Get
            };

            var json = JsonSerializer.Serialize(config);
            var deserialized = JsonSerializer.Deserialize<DockerBuildConfig>(json);

            return (deserialized!.ImageName == config.ImageName)
                .And(deserialized.Tag == config.Tag);
        });
}
```

## Checklist for New Components

- [ ] Interface extends `IUnifyBuildConfig`
- [ ] Targets have `.Description()` for CLI help
- [ ] Null/disabled config handled gracefully (skip, don't fail)
- [ ] External tools detected before use with clear install instructions on failure
- [ ] Config class added to `BuildJsonConfig` (nullable, optional)
- [ ] Context record added to `BuildContext` (nullable, optional)
- [ ] Mapping added to `BuildContextLoader`
- [ ] JSON schema updated in `build.config.schema.json`
- [ ] Interface composed in `Build.cs`
- [ ] Unit tests for config defaults and path resolution
- [ ] Integration test for end-to-end config loading
- [ ] Logging uses `Serilog.Log` (Information, Warning, Error, Debug)
