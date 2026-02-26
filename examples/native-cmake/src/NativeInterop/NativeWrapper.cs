using System.Runtime.InteropServices;

namespace NativeInterop;

/// <summary>
/// P/Invoke wrapper for the native mylib library.
/// </summary>
public static class MyLib
{
    [DllImport("mylib", CallingConvention = CallingConvention.Cdecl)]
    public static extern int mylib_add(int a, int b);
}
