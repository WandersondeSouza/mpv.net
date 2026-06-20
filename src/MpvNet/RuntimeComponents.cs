using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MpvNet;

/// <summary>
/// Provides the stable public entry points for resolving and updating optional
/// runtime components used by MPV.NET.
/// </summary>
public static class RuntimeComponents
{
    public static string ComponentsFolder => RuntimeComponentPaths.ComponentsFolder;
    public static string TempFolder => RuntimeComponentPaths.TempFolder;

    public static void RegisterNativeResolver()
    {
        NativeLibrary.SetDllImportResolver(typeof(RuntimeComponents).Assembly, ResolveNativeLibrary);
    }

    public static Task EnsureComponentsAsync(CancellationToken cancellationToken = default)
    {
        return RuntimeComponentService.EnsureComponentsAsync(cancellationToken);
    }

    public static string ResolveComponentPath(string fileName)
    {
        return RuntimeComponentPathResolver.Resolve(fileName);
    }

    static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        string fileName = libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? libraryName
            : libraryName + ".dll";

        string candidate = ResolveComponentPath(fileName);
        return File.Exists(candidate)
            ? NativeLibrary.Load(candidate, assembly, searchPath)
            : IntPtr.Zero;
    }
}
