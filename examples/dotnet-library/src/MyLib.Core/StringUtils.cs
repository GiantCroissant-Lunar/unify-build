using MyLib.Abstractions;

namespace MyLib.Core;

/// <summary>
/// Provides common string utility operations.
/// </summary>
public class UpperCaseProcessor : IStringProcessor
{
    public string Process(string input) => input.ToUpperInvariant();
}
