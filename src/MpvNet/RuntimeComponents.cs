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

    public static void EnsureComponentsFolderOnPath()
    {
        string componentsFolder = Path.TrimEndingDirectorySeparator(ComponentsFolder);
        string? currentPath = Environment.GetEnvironmentVariable("PATH");

        if (!string.IsNullOrWhiteSpace(currentPath) &&
            currentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => Path.TrimEndingDirectorySeparator(path))
                .Any(path => string.Equals(path, componentsFolder, StringComparison.OrdinalIgnoreCase)))
        {
            Log.Debug($"Runtime component folder already present in process PATH. path='{Log.SafeValue(componentsFolder)}'");
            return;
        }

        string updatedPath = string.IsNullOrWhiteSpace(currentPath)
            ? componentsFolder
            : componentsFolder + Path.PathSeparator + currentPath;

        Environment.SetEnvironmentVariable("PATH", updatedPath);
        Log.Debug($"Added runtime component folder to process PATH. path='{Log.SafeValue(componentsFolder)}'");
    }

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
