using System;
using System.IO;
using System.Text.Json;

var json = File.ReadAllText("build.config.json");
Console.WriteLine("JSON loaded, length: " + json.Length);

var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var doc = JsonDocument.Parse(json);
var root = doc.RootElement;

Console.WriteLine("Has godotBuild: " + root.TryGetProperty("godotBuild", out var gb));
if (root.TryGetProperty("godotBuild", out var godot))
{
    Console.WriteLine("godotBuild.projectRoot: " + godot.GetProperty("projectRoot").GetString());
}
