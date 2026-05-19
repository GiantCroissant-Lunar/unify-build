using System.IO;
using System.Text.Json;
using NJsonSchema;
using NJsonSchema.Generation;
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

            var settings = new SystemTextJsonSchemaGeneratorSettings
            {
                SerializerOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                },
                SchemaType = SchemaType.JsonSchema,
                FlattenInheritanceHierarchy = true,
            };

            var schema = JsonSchema.FromType<BuildJsonConfig>(settings);
            schema.Title = "UnifyBuild build.config";
            schema.Id = "./build.config.schema.json";

            File.WriteAllText(outputFile, schema.ToJson());
            Serilog.Log.Information("Schema written: {OutputFile}", outputFile);
        });
}
