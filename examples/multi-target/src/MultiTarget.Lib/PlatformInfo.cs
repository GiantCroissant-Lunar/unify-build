namespace MultiTarget.Lib;

/// <summary>
/// Provides platform-specific information using conditional compilation.
/// </summary>
public static class PlatformInfo
{
    public static string RuntimeName =>
#if NET8_0_OR_GREATER
        ".NET 8+";
#elif NET6_0_OR_GREATER
        ".NET 6+";
#else
        ".NET Standard 2.1";
#endif

    public static bool SupportsSpan =>
#if NETSTANDARD2_1
        true;  // Span<T> is available in netstandard2.1
#else
        true;
#endif
}
