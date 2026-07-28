using System.Text.Json;
using NJsonSchema;
using NJsonSchema.Generation;

namespace UnifyBuild.Nuke;

/// <summary>
/// Generates the canonical JSON Schema for <see cref="BuildJsonConfig"/>.
/// </summary>
internal static class BuildConfigSchemaGenerator
{
    internal static string Generate()
    {
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

        return schema.ToJson()
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd('\r', '\n')
            + "\n";
    }
}
