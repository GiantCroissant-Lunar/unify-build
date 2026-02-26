using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace UnifyBuild.Nuke.Extensibility;

/// <summary>
/// Result of loading external component types from plugin assemblies.
/// </summary>
public sealed record ExternalComponentResult(
    IReadOnlyList<Type> ComponentTypes,
    IReadOnlyList<string> Errors
);

/// <summary>
/// Loads custom component types from external assemblies that implement <see cref="IUnifyBuildConfig"/>.
/// </summary>
public static class ExternalComponentLoader
{
    /// <summary>
    /// Default plugin directory relative to repo root.
    /// </summary>
    public const string DefaultPluginDirectory = "build/plugins";

    /// <summary>
    /// Loads component types from the specified paths.
    /// Each path can be a .dll file or a directory containing .dll files.
    /// </summary>
    /// <param name="repoRoot">Repository root directory.</param>
    /// <param name="paths">Paths to assemblies or directories (relative to repo root).</param>
    /// <returns>Result containing discovered component types and any errors encountered.</returns>
    public static ExternalComponentResult LoadFromPaths(string repoRoot, IEnumerable<string> paths)
    {
        var componentTypes = new List<Type>();
        var errors = new List<string>();

        foreach (var path in paths)
        {
            var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(repoRoot, path);

            if (File.Exists(fullPath) && fullPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                LoadAssembly(fullPath, componentTypes, errors);
            }
            else if (Directory.Exists(fullPath))
            {
                var dlls = Directory.GetFiles(fullPath, "*.dll", SearchOption.TopDirectoryOnly);
                foreach (var dll in dlls)
                {
                    LoadAssembly(dll, componentTypes, errors);
                }
            }
            else
            {
                errors.Add($"Plugin path not found: {fullPath}");
            }
        }

        return new ExternalComponentResult(componentTypes.AsReadOnly(), errors.AsReadOnly());
    }

    /// <summary>
    /// Loads component types from the default plugin directory and any configured paths.
    /// </summary>
    /// <param name="repoRoot">Repository root directory.</param>
    /// <param name="config">Extensions configuration from build.config.json.</param>
    /// <returns>Result containing discovered component types and any errors encountered.</returns>
    public static ExternalComponentResult LoadFromConfig(string repoRoot, ExtensionsConfig? config)
    {
        var paths = new List<string>();

        if (config?.AutoLoadPlugins == true)
        {
            paths.Add(DefaultPluginDirectory);
        }

        if (config?.PluginPaths is not null)
        {
            paths.AddRange(config.PluginPaths);
        }

        if (paths.Count == 0)
        {
            return new ExternalComponentResult(Array.Empty<Type>(), Array.Empty<string>());
        }

        return LoadFromPaths(repoRoot, paths);
    }

    /// <summary>
    /// Validates that a type properly implements <see cref="IUnifyBuildConfig"/>.
    /// </summary>
    /// <param name="type">The type to validate.</param>
    /// <returns>True if the type implements IUnifyBuildConfig and is a valid component type.</returns>
    public static bool ValidateComponentType(Type type)
    {
        return type is { IsAbstract: false, IsInterface: false }
               && typeof(IUnifyBuildConfig).IsAssignableFrom(type);
    }

    private static void LoadAssembly(string assemblyPath, List<Type> componentTypes, List<string> errors)
    {
        try
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            var types = ScanAssemblyForComponents(assembly);
            componentTypes.AddRange(types);
        }
        catch (BadImageFormatException)
        {
            errors.Add($"Not a valid .NET assembly: {assemblyPath}");
        }
        catch (FileLoadException ex)
        {
            errors.Add($"Failed to load assembly '{assemblyPath}': {ex.Message}");
        }
        catch (Exception ex)
        {
            errors.Add($"Error loading '{assemblyPath}': {ex.Message}");
        }
    }

    private static IEnumerable<Type> ScanAssemblyForComponents(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes()
                .Where(ValidateComponentType);
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Return whatever types we could load
            return ex.Types
                .Where(t => t is not null && ValidateComponentType(t))!;
        }
    }
}
