using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nuke.Common.IO;

namespace UnifyBuild.Nuke.Performance;

/// <summary>
/// Result of building a single project group.
/// </summary>
public sealed record GroupBuildResult(
    string GroupName,
    bool Success,
    TimeSpan Duration,
    string? Error = null
);

/// <summary>
/// Orchestrates parallel builds of independent project groups.
/// Groups are considered independent when they have no overlapping source directories.
/// </summary>
public sealed class ParallelBuildOrchestrator
{
    private readonly int _maxParallelism;

    /// <summary>
    /// Creates a new orchestrator with the specified max parallelism.
    /// </summary>
    /// <param name="maxParallelism">
    /// Maximum number of groups to build concurrently.
    /// Values &lt;= 0 default to <see cref="Environment.ProcessorCount"/>.
    /// </param>
    public ParallelBuildOrchestrator(int? maxParallelism = null)
    {
        _maxParallelism = maxParallelism is > 0
            ? maxParallelism.Value
            : Environment.ProcessorCount;
    }

    /// <summary>Effective max parallelism used by this orchestrator.</summary>
    public int MaxParallelism => _maxParallelism;

    /// <summary>
    /// Determines which project groups are independent (no shared source directories).
    /// Two groups are independent if their normalized source directory paths do not
    /// overlap (neither is a prefix of the other).
    /// </summary>
    /// <param name="groups">Dictionary of group name → <see cref="ProjectGroup"/>.</param>
    /// <param name="repoRoot">Repository root for resolving relative paths.</param>
    /// <returns>
    /// A list of sets, where each set contains group names that are mutually independent
    /// and can be built in parallel.
    /// </returns>
    public static List<HashSet<string>> AnalyzeIndependence(
        Dictionary<string, ProjectGroup> groups,
        AbsolutePath repoRoot)
    {
        if (groups.Count == 0)
            return new List<HashSet<string>>();

        // Resolve and normalize source directories
        var resolvedDirs = groups.ToDictionary(
            kvp => kvp.Key,
            kvp => NormalizePath(Path.Combine((string)repoRoot, kvp.Value.SourceDir)),
            StringComparer.OrdinalIgnoreCase);

        // Build adjacency: groups that share/overlap directories are dependent
        var dependentPairs = new HashSet<(string, string)>();
        var names = resolvedDirs.Keys.ToList();

        for (var i = 0; i < names.Count; i++)
        {
            for (var j = i + 1; j < names.Count; j++)
            {
                var dirA = resolvedDirs[names[i]];
                var dirB = resolvedDirs[names[j]];

                if (DirectoriesOverlap(dirA, dirB))
                {
                    dependentPairs.Add((names[i], names[j]));
                }
            }
        }

        // Greedy coloring: assign groups to parallel batches
        // Groups in the same batch are all mutually independent
        var batches = new List<HashSet<string>>();
        var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            if (assigned.Contains(name))
                continue;

            var batch = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { name };
            assigned.Add(name);

            foreach (var candidate in names)
            {
                if (assigned.Contains(candidate))
                    continue;

                // Check candidate is independent of all current batch members
                var isIndependent = batch.All(member =>
                    !dependentPairs.Contains((member, candidate)) &&
                    !dependentPairs.Contains((candidate, member)));

                if (isIndependent)
                {
                    batch.Add(candidate);
                    assigned.Add(candidate);
                }
            }

            batches.Add(batch);
        }

        return batches;
    }

    /// <summary>
    /// Builds project groups in parallel where possible.
    /// Independent groups run concurrently; dependent groups run sequentially in batches.
    /// </summary>
    /// <param name="groups">Dictionary of group name → <see cref="ProjectGroup"/>.</param>
    /// <param name="repoRoot">Repository root for resolving paths.</param>
    /// <param name="buildAction">
    /// The action to execute for each group. Receives (groupName, group) and returns success.
    /// Must be thread-safe and not share mutable state.
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Results for each group.</returns>
    public async Task<List<GroupBuildResult>> BuildAsync(
        Dictionary<string, ProjectGroup> groups,
        AbsolutePath repoRoot,
        Func<string, ProjectGroup, Task<bool>> buildAction,
        CancellationToken cancellationToken = default)
    {
        var results = new List<GroupBuildResult>();

        if (groups.Count == 0)
            return results;

        var batches = AnalyzeIndependence(groups, repoRoot);

        foreach (var batch in batches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchGroups = batch
                .Where(name => groups.ContainsKey(name))
                .Select(name => (Name: name, Group: groups[name]))
                .ToList();

            if (batchGroups.Count == 1)
            {
                // Single group — run directly
                var (name, group) = batchGroups[0];
                results.Add(await BuildSingleGroupAsync(name, group, buildAction));
            }
            else
            {
                // Multiple independent groups — run in parallel
                var semaphore = new SemaphoreSlim(_maxParallelism);
                var tasks = batchGroups.Select(async bg =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        return await BuildSingleGroupAsync(bg.Name, bg.Group, buildAction);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToList();

                var batchResults = await Task.WhenAll(tasks);
                results.AddRange(batchResults);
            }
        }

        return results;
    }

    private static async Task<GroupBuildResult> BuildSingleGroupAsync(
        string name,
        ProjectGroup group,
        Func<string, ProjectGroup, Task<bool>> buildAction)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var success = await buildAction(name, group);
            sw.Stop();
            return new GroupBuildResult(name, success, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new GroupBuildResult(name, false, sw.Elapsed, ex.Message);
        }
    }

    /// <summary>
    /// Checks whether two directory paths overlap (one is a prefix of the other, or they are equal).
    /// </summary>
    internal static bool DirectoriesOverlap(string dirA, string dirB)
    {
        // Ensure trailing separator for prefix comparison
        var a = EnsureTrailingSeparator(dirA);
        var b = EnsureTrailingSeparator(dirB);

        return a.StartsWith(b, StringComparison.OrdinalIgnoreCase) ||
               b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
