using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using MpvNet.Native;

namespace MpvNet;

/// <summary>
/// Provides the stable public entry points for resolving and updating optional
/// runtime components used by MPV.NET.
/// </summary>
public static class RuntimeComponents
{
    static int _nativeResolverRegistered;

    public static string ComponentsFolder => RuntimeComponentPaths.ComponentsFolder;
    public static string TempFolder => RuntimeComponentPaths.TempFolder;

    public static void EnsureComponentsFolderOnPath()
    {
        string? currentPath = Environment.GetEnvironmentVariable("PATH");
        string updatedPath = WindowsPathEnvironment.PrependEntries(currentPath,
            RuntimeComponentPaths.CurrentFolder, ComponentsFolder, AppPaths.Startup);
        if (updatedPath == currentPath)
            return;
        Environment.SetEnvironmentVariable("PATH", updatedPath);
        Log.Debug("Prepared current, legacy and application component directories at the front of the process PATH.");
    }

    public static void RegisterNativeResolver()
    {
        if (Interlocked.CompareExchange(ref _nativeResolverRegistered, 1, 0) != 0)
            return;

        try
        {
            NativeLibrary.SetDllImportResolver(typeof(LibMpv).Assembly, ResolveNativeLibrary);
        }
        catch
        {
            Volatile.Write(ref _nativeResolverRegistered, 0);
            throw;
        }
    }

    internal static string? ResolveNativeLibraryPath(string libraryName)
    {
        string fileName = libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? libraryName
            : libraryName + ".dll";

        if (!fileName.Equals("MediaInfo.dll", StringComparison.OrdinalIgnoreCase))
            return null;

        string candidate = ResolveComponentPath(fileName);
        return File.Exists(candidate) ? candidate : null;
    }

    public static Task EnsureComponentsAsync(CancellationToken cancellationToken = default)
    {
        return RuntimeComponentService.EnsureComponentsAsync(cancellationToken);
    }

    public static string DiagnoseLibMpv()
    {
        LibMpvDiagnosticResult diagnostic = LibMpvDiagnostics.Run();
        LibMpvLoadDiagnostics selection = diagnostic.Selection;
        return
            $"CPU x86-64-v3 compatible: {selection.CpuCompatibleWithX86_64V3}; " +
            $"selected DLL: {selection.LoadedFile}; " +
            $"path: {selection.LoadedPath}; " +
            $"fallback: {selection.FallbackUsed}; " +
            $"libmpv API: 0x{diagnostic.ClientApiVersion:X}; " +
            $"mpv_create: {diagnostic.MpvCreateSucceeded}";
    }

    public static string DiagnoseComponents()
    {
        string[] componentNames = ["ffmpeg.exe", "ffplay.exe", "ffprobe.exe", "mpvnet.com", "yt-dlp.exe", "deno.exe", "MediaInfo.dll"];
        string libMpvReport;
        try
        {
            libMpvReport = "libmpv: " + DiagnoseLibMpv();
        }
        catch (Exception ex)
        {
            libMpvReport = $"libmpv: unavailable; reason={Log.SafeValue(ex.Message)}";
        }

        string componentReport = string.Join(
            Environment.NewLine,
            componentNames.Select(name =>
            {
                ComponentResolutionResult result = ResolveComponent(name);
                return $"{name}: source={result.Source}; path={result.ResolvedPath ?? "<unavailable>"}; " +
                    $"valid={result.IsValid}; version={result.Version ?? "<unknown>"}; " +
                    $"reason={result.DiagnosticMessage ?? "<none>"}";
            }));

        return AppPaths.DiagnosePaths() + Environment.NewLine + libMpvReport + Environment.NewLine + componentReport + Environment.NewLine +
            RuntimeComponentService.DescribeCacheStatus() + Environment.NewLine +
            OnlineMediaDiagnostics.BuildReport(
                ResolveComponent("yt-dlp.exe"),
                ResolveComponent("ffmpeg.exe"));
    }

    public static string ResolveComponentPath(string fileName)
    {
        return RuntimeComponentPathResolver.Resolve(fileName);
    }

    public static ComponentResolutionResult ResolveComponent(string fileName)
    {
        return RuntimeComponentPathResolver.ResolveResult(fileName);
    }

    internal static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (LibMpvRuntime.IsExpectedLibraryName(libraryName))
            return LibMpvRuntime.LoadSelectedLibrary();

        string? candidate = ResolveNativeLibraryPath(libraryName);
        return candidate is not null
            ? NativeLibrary.Load(candidate)
            : IntPtr.Zero;
    }
}
