using System.IO;
using Nuke.Common;
using Nuke.Common.IO;

namespace UnifyBuild.Nuke;

/// <summary>
/// Schema generation target: generates JSON Schema for build.config.json from the
/// compiled <c>BuildJsonConfig</c> type via NJsonSchema reflection.
/// </summary>
public interface IUnifySchemaGeneration : IUnifyBuildConfig
{
    /// <summary>
    /// Generate JSON schema from BuildJsonConfig and write it to
    /// build/_artifacts/build.config.schema.json. Runs before Pack so the
    /// generated schema ships with the nupkg.
    /// </summary>
    Target GenerateSchema => _ => _
        .Description("Generate JSON schema from BuildJsonConfig via NJsonSchema reflection")
        .OnlyWhenDynamic(() => (RootDirectory / "dotnet" / "src" / "UnifyBuild.Nuke" / "BuildConfigJson.cs").FileExists())
        .Before<IUnifyPack>(x => x.Pack)
        .Executes(() =>
        {
            var outputDir = RootDirectory / "build" / "_artifacts";
            var outputFile = outputDir / "build.config.schema.json";
            outputDir.CreateDirectory();

            Serilog.Log.Information("Generating JSON schema from BuildJsonConfig via NJsonSchema...");

            File.WriteAllText(outputFile, BuildConfigSchemaGenerator.Generate());
            Serilog.Log.Information("Schema written: {OutputFile}", outputFile);
        });
}
