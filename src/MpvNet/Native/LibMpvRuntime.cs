using System.Runtime.InteropServices;
using System.Threading;

namespace MpvNet.Native;

internal enum LibMpvBuildPreference
{
    Auto,
    Normal,
    X86_64V3
}

internal sealed record CpuFeatureSet
{
    public bool Sse3 { get; init; } = true;
    public bool Ssse3 { get; init; } = true;
    public bool Sse41 { get; init; } = true;
    public bool Sse42 { get; init; } = true;
    public bool Popcnt { get; init; } = true;
    public bool Avx { get; init; } = true;
    public bool Avx2 { get; init; } = true;
    public bool F16c { get; init; } = true;
    public bool Fma { get; init; } = true;
    public bool Bmi1 { get; init; } = true;
    public bool Bmi2 { get; init; } = true;
    public bool Lzcnt { get; init; } = true;
    public bool Movbe { get; init; } = true;

    public bool SupportsX86_64V3 =>
        Sse3 && Ssse3 && Sse41 && Sse42 && Popcnt &&
        Avx && Avx2 && F16c && Fma && Bmi1 && Bmi2 && Lzcnt && Movbe;

    public static CpuFeatureSet Current { get; } = new()
    {
        Sse3 = System.Runtime.Intrinsics.X86.Sse3.IsSupported,
        Ssse3 = System.Runtime.Intrinsics.X86.Ssse3.IsSupported,
        Sse41 = System.Runtime.Intrinsics.X86.Sse41.IsSupported,
        Sse42 = System.Runtime.Intrinsics.X86.Sse42.IsSupported,
        Popcnt = System.Runtime.Intrinsics.X86.Popcnt.IsSupported,
        Avx = System.Runtime.Intrinsics.X86.Avx.IsSupported,
        Avx2 = System.Runtime.Intrinsics.X86.Avx2.IsSupported,
        // .NET exposes the remaining x86-64-v3 requirements as intrinsics.
        // F16C and MOVBE have no dedicated intrinsic type, so read only their
        // documented CPUID leaf-1 ECX bits as the narrow fallback.
        F16c = HasLeaf1EcxBit(29),
        Fma = System.Runtime.Intrinsics.X86.Fma.IsSupported,
        Bmi1 = System.Runtime.Intrinsics.X86.Bmi1.IsSupported,
        Bmi2 = System.Runtime.Intrinsics.X86.Bmi2.IsSupported,
        Lzcnt = System.Runtime.Intrinsics.X86.Lzcnt.IsSupported,
        Movbe = HasLeaf1EcxBit(22)
    };

    static bool HasLeaf1EcxBit(int bit)
    {
        if (!System.Runtime.Intrinsics.X86.X86Base.IsSupported)
            return false;

        int ecx = System.Runtime.Intrinsics.X86.X86Base.CpuId(1, 0).Ecx;
        return (ecx & (1 << bit)) != 0;
    }
}

internal interface ICpuFeatureProvider
{
    bool Is64BitProcess { get; }
    bool IsX64Architecture { get; }
    CpuFeatureSet Features { get; }
}

internal sealed class RuntimeCpuFeatureProvider : ICpuFeatureProvider
{
    public bool Is64BitProcess => Environment.Is64BitProcess;
    public bool IsX64Architecture => RuntimeInformation.ProcessArchitecture == Architecture.X64;
    public CpuFeatureSet Features => CpuFeatureSet.Current;
}

internal interface INativeLibraryLoader
{
    bool TryLoad(string libraryPath, out IntPtr handle, out string failureReason);
}

internal sealed class NativeLibraryLoader : INativeLibraryLoader
{
    public bool TryLoad(string libraryPath, out IntPtr handle, out string failureReason)
    {
        handle = IntPtr.Zero;
        failureReason = "";

        if (!File.Exists(libraryPath))
        {
            failureReason = "file does not exist";
            return false;
        }

        try
        {
            if (!NativeLibrary.TryLoad(libraryPath, out handle) || handle == IntPtr.Zero)
            {
                failureReason = "NativeLibrary.TryLoad returned false";
                return false;
            }

            return true;
        }
        catch (BadImageFormatException exception)
        {
            failureReason = exception.Message;
            return false;
        }
        catch (FileLoadException exception)
        {
            failureReason = exception.Message;
            return false;
        }
        catch (DllNotFoundException exception)
        {
            failureReason = exception.Message;
            return false;
        }
    }
}

internal sealed record LibMpvLoadDiagnostics(
    bool Is64BitProcess,
    bool IsX64Architecture,
    bool CpuCompatibleWithX86_64V3,
    LibMpvBuildPreference Preference,
    string PreferredFile,
    string LoadedFile,
    string LoadedPath,
    bool FallbackUsed,
    string? FallbackReason,
    string? ConfigurationWarning);

internal sealed record LibMpvLoadResult(IntPtr Handle, LibMpvLoadDiagnostics Diagnostics);

internal sealed class LibMpvLibraryLoader
{
    internal const string NormalFileName = "libmpv-2.dll";
    internal const string X86_64V3FileName = "libmpv-2-v3.dll";

    readonly ICpuFeatureProvider _cpuFeatureProvider;
    readonly INativeLibraryLoader _nativeLibraryLoader;
    readonly Func<string> _appBaseDirectory;
    readonly Func<string?> _preferenceValue;

    public LibMpvLibraryLoader(
        ICpuFeatureProvider cpuFeatureProvider,
        INativeLibraryLoader nativeLibraryLoader,
        Func<string> appBaseDirectory,
        Func<string?> preferenceValue)
    {
        _cpuFeatureProvider = cpuFeatureProvider;
        _nativeLibraryLoader = nativeLibraryLoader;
        _appBaseDirectory = appBaseDirectory;
        _preferenceValue = preferenceValue;
    }

    public LibMpvLoadResult Load()
    {
        if (!_cpuFeatureProvider.Is64BitProcess || !_cpuFeatureProvider.IsX64Architecture)
        {
            throw new PlatformNotSupportedException(
                "MPV.NET is distributed only for x64. Use an x64 process and x64 operating system.");
        }

        bool cpuCompatibleWithV3 = _cpuFeatureProvider.Features.SupportsX86_64V3;
        LibMpvBuildPreference preference = GetPreference(out string? configurationWarning);
        if (preference == LibMpvBuildPreference.X86_64V3 && !cpuCompatibleWithV3)
        {
            throw new PlatformNotSupportedException(
                "MPVNET_FORCE_LIBMPV_VARIANT=x86_64-v3 requires an x86-64-v3 compatible CPU.");
        }

        string baseDirectory = Path.GetFullPath(_appBaseDirectory());
        string normalPath = Path.Combine(baseDirectory, NormalFileName);
        string v3Path = Path.Combine(baseDirectory, X86_64V3FileName);
        bool shouldTryV3 = preference != LibMpvBuildPreference.Normal && cpuCompatibleWithV3;
        string preferredFile = shouldTryV3 ? X86_64V3FileName : NormalFileName;
        string? v3Failure = null;

        if (shouldTryV3)
        {
            if (_nativeLibraryLoader.TryLoad(v3Path, out IntPtr v3Handle, out string failureReason))
            {
                return CreateResult(
                    v3Handle,
                    cpuCompatibleWithV3,
                    preference,
                    preferredFile,
                    X86_64V3FileName,
                    v3Path,
                    fallbackUsed: false,
                    fallbackReason: null,
                    configurationWarning);
            }

            v3Failure = failureReason;
        }

        if (_nativeLibraryLoader.TryLoad(normalPath, out IntPtr normalHandle, out string normalFailure))
        {
            return CreateResult(
                normalHandle,
                cpuCompatibleWithV3,
                preference,
                preferredFile,
                NormalFileName,
                normalPath,
                fallbackUsed: shouldTryV3,
                fallbackReason: v3Failure,
                configurationWarning);
        }

        string v3Attempt = shouldTryV3
            ? $"{X86_64V3FileName} at '{v3Path}' failed: {v3Failure}. "
            : "";
        throw new DllNotFoundException(
            $"Could not load libmpv. {v3Attempt}{NormalFileName} at '{normalPath}' failed: {normalFailure}.");
    }

    LibMpvLoadResult CreateResult(
        IntPtr handle,
        bool cpuCompatibleWithV3,
        LibMpvBuildPreference preference,
        string preferredFile,
        string loadedFile,
        string loadedPath,
        bool fallbackUsed,
        string? fallbackReason,
        string? configurationWarning)
    {
        return new(
            handle,
            new(
                _cpuFeatureProvider.Is64BitProcess,
                _cpuFeatureProvider.IsX64Architecture,
                cpuCompatibleWithV3,
                preference,
                preferredFile,
                loadedFile,
                loadedPath,
                fallbackUsed,
                fallbackReason,
                configurationWarning));
    }

    LibMpvBuildPreference GetPreference(out string? configurationWarning)
    {
        configurationWarning = null;
        string? value = _preferenceValue();
        if (string.IsNullOrWhiteSpace(value) || value.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return LibMpvBuildPreference.Auto;

        if (value.Equals("normal", StringComparison.OrdinalIgnoreCase))
            return LibMpvBuildPreference.Normal;

        if (value.Equals("x86_64-v3", StringComparison.OrdinalIgnoreCase))
            return LibMpvBuildPreference.X86_64V3;

        configurationWarning =
            $"Ignoring invalid MPVNET_FORCE_LIBMPV_VARIANT value '{Log.SafeValue(value)}'; using auto.";
        return LibMpvBuildPreference.Auto;
    }
}

internal static class LibMpvRuntime
{
    static readonly Lazy<LibMpvLoadResult> LoadResult = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static LibMpvLoadDiagnostics Diagnostics => LoadResult.Value.Diagnostics;

    public static IntPtr LoadSelectedLibrary() => LoadResult.Value.Handle;

    public static bool IsExpectedLibraryName(string libraryName)
    {
        return libraryName.Equals("libmpv-2", StringComparison.OrdinalIgnoreCase) ||
            libraryName.Equals(LibMpvLibraryLoader.NormalFileName, StringComparison.OrdinalIgnoreCase);
    }

    static LibMpvLoadResult Load()
    {
        LibMpvLibraryLoader loader = new(
            new RuntimeCpuFeatureProvider(),
            new NativeLibraryLoader(),
            () => AppContext.BaseDirectory,
            () => Environment.GetEnvironmentVariable("MPVNET_FORCE_LIBMPV_VARIANT"));
        LibMpvLoadResult result = loader.Load();
        LibMpvLoadDiagnostics diagnostics = result.Diagnostics;

        if (diagnostics.ConfigurationWarning is not null)
            Log.Debug(diagnostics.ConfigurationWarning);

        Log.Debug(
            $"libmpv selection completed. cpuV3={diagnostics.CpuCompatibleWithX86_64V3}, " +
            $"preferred='{diagnostics.PreferredFile}', loaded='{diagnostics.LoadedFile}', " +
            $"path='{Log.SafeValue(diagnostics.LoadedPath)}', fallback={diagnostics.FallbackUsed}, " +
            $"fallbackReason='{Log.SafeValue(diagnostics.FallbackReason)}'");
        return result;
    }
}

internal sealed record LibMpvDiagnosticResult(
    LibMpvLoadDiagnostics Selection,
    ulong ClientApiVersion,
    bool MpvCreateSucceeded);

internal static class LibMpvDiagnostics
{
    public static LibMpvDiagnosticResult Run()
    {
        RuntimeComponents.RegisterNativeResolver();
        LibMpvLoadDiagnostics selection = LibMpvRuntime.Diagnostics;
        ulong clientApiVersion = LibMpv.mpv_client_api_version();
        IntPtr handle = LibMpv.mpv_create();
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("libmpv diagnostic mpv_create returned a null handle.");

        try
        {
            Log.Debug(
                $"libmpv diagnostic completed. cpuV3={selection.CpuCompatibleWithX86_64V3}, " +
                $"loaded='{selection.LoadedFile}', api=0x{clientApiVersion:X}, mpvCreateSucceeded=true");
            return new(selection, clientApiVersion, MpvCreateSucceeded: true);
        }
        finally
        {
            LibMpv.mpv_destroy(handle);
        }
    }
}
