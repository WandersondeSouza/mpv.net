using System;
using System.Collections.Generic;
using System.IO;

using MpvNet;
using MpvNet.Native;

using Xunit;

namespace MpvNet.Tests;

public sealed class LibMpvRuntimeTests
{
    static readonly string PackageDirectory = Path.Combine(Path.GetTempPath(), "mpvnet-libmpv-runtime-tests");

    [Fact]
    public void CompatibleCpuPrefersV3WithoutLoadingNormal()
    {
        FakeNativeLibraryLoader nativeLoader = new((LibMpvLibraryLoader.X86_64V3FileName, true, ""));

        LibMpvLoadResult result = CreateLoader(nativeLoader).Load();

        Assert.Equal(LibMpvLibraryLoader.X86_64V3FileName, result.Diagnostics.LoadedFile);
        Assert.False(result.Diagnostics.FallbackUsed);
        Assert.Single(nativeLoader.AttemptedFiles);
        Assert.Equal(LibMpvLibraryLoader.X86_64V3FileName, nativeLoader.AttemptedFiles[0]);
    }

    [Fact]
    public void IncompatibleCpuLoadsNormalWithoutTryingV3()
    {
        FakeNativeLibraryLoader nativeLoader = new((LibMpvLibraryLoader.NormalFileName, true, ""));

        LibMpvLoadResult result = CreateLoader(nativeLoader, features: new CpuFeatureSet { Avx2 = false }).Load();

        Assert.Equal(LibMpvLibraryLoader.NormalFileName, result.Diagnostics.LoadedFile);
        Assert.False(result.Diagnostics.FallbackUsed);
        Assert.Equal([LibMpvLibraryLoader.NormalFileName], nativeLoader.AttemptedFiles);
    }

    [Fact]
    public void CompatibleCpuFallsBackWhenV3IsMissing()
    {
        FakeNativeLibraryLoader nativeLoader = new(
            (LibMpvLibraryLoader.X86_64V3FileName, false, "simulated load failure"),
            (LibMpvLibraryLoader.NormalFileName, true, ""));

        LibMpvLoadResult result = CreateLoader(nativeLoader).Load();

        Assert.Equal(LibMpvLibraryLoader.NormalFileName, result.Diagnostics.LoadedFile);
        Assert.True(result.Diagnostics.FallbackUsed);
        Assert.Equal("simulated load failure", result.Diagnostics.FallbackReason);
        Assert.Equal(
            [LibMpvLibraryLoader.X86_64V3FileName, LibMpvLibraryLoader.NormalFileName],
            nativeLoader.AttemptedFiles);
    }

    [Fact]
    public void CompatibleCpuFallsBackWhenV3CannotLoad()
    {
        FakeNativeLibraryLoader nativeLoader = new(
            (LibMpvLibraryLoader.X86_64V3FileName, false, "bad image"),
            (LibMpvLibraryLoader.NormalFileName, true, ""));

        LibMpvLoadResult result = CreateLoader(nativeLoader).Load();

        Assert.True(result.Diagnostics.FallbackUsed);
        Assert.Equal("bad image", result.Diagnostics.FallbackReason);
        Assert.Equal(LibMpvLibraryLoader.NormalFileName, result.Diagnostics.LoadedFile);
    }

    [Fact]
    public void CompatibleCpuCanRunWithV3WhenNormalIsMissing()
    {
        FakeNativeLibraryLoader nativeLoader = new((LibMpvLibraryLoader.X86_64V3FileName, true, ""));

        LibMpvLoadResult result = CreateLoader(nativeLoader).Load();

        Assert.Equal(LibMpvLibraryLoader.X86_64V3FileName, result.Diagnostics.LoadedFile);
        Assert.Equal([LibMpvLibraryLoader.X86_64V3FileName], nativeLoader.AttemptedFiles);
    }

    [Fact]
    public void MissingNormalFailsClearlyForIncompatibleCpu()
    {
        FakeNativeLibraryLoader nativeLoader = new();

        DllNotFoundException exception = Assert.Throws<DllNotFoundException>(
            () => CreateLoader(nativeLoader, features: new CpuFeatureSet { Avx2 = false }).Load());

        Assert.Contains(LibMpvLibraryLoader.NormalFileName, exception.Message);
        Assert.Equal([LibMpvLibraryLoader.NormalFileName], nativeLoader.AttemptedFiles);
    }

    [Fact]
    public void MissingBothFilesIncludeBothAttemptsInFinalError()
    {
        FakeNativeLibraryLoader nativeLoader = new();

        DllNotFoundException exception = Assert.Throws<DllNotFoundException>(() => CreateLoader(nativeLoader).Load());

        Assert.Contains(LibMpvLibraryLoader.X86_64V3FileName, exception.Message);
        Assert.Contains(LibMpvLibraryLoader.NormalFileName, exception.Message);
    }

    [Fact]
    public void NonX64DistributionFailsBeforeSelectingAnyFile()
    {
        FakeNativeLibraryLoader nativeLoader = new();

        PlatformNotSupportedException exception = Assert.Throws<PlatformNotSupportedException>(
            () => CreateLoader(nativeLoader, is64BitProcess: false).Load());

        Assert.Contains("x64", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(nativeLoader.AttemptedFiles);
    }

    [Fact]
    public void ForcedNormalSkipsV3()
    {
        FakeNativeLibraryLoader nativeLoader = new((LibMpvLibraryLoader.NormalFileName, true, ""));

        LibMpvLoadResult result = CreateLoader(nativeLoader, preference: "normal").Load();

        Assert.Equal(LibMpvBuildPreference.Normal, result.Diagnostics.Preference);
        Assert.Equal([LibMpvLibraryLoader.NormalFileName], nativeLoader.AttemptedFiles);
    }

    [Fact]
    public void ForcedV3RejectsIncompatibleCpu()
    {
        FakeNativeLibraryLoader nativeLoader = new();

        Assert.Throws<PlatformNotSupportedException>(
            () => CreateLoader(nativeLoader, features: new CpuFeatureSet { Avx2 = false }, preference: "x86_64-v3").Load());
        Assert.Empty(nativeLoader.AttemptedFiles);
    }

    [Fact]
    public void InvalidPreferenceUsesAutoAndRecordsWarning()
    {
        FakeNativeLibraryLoader nativeLoader = new((LibMpvLibraryLoader.X86_64V3FileName, true, ""));

        LibMpvLoadResult result = CreateLoader(nativeLoader, preference: "invalid").Load();

        Assert.Equal(LibMpvBuildPreference.Auto, result.Diagnostics.Preference);
        Assert.NotNull(result.Diagnostics.ConfigurationWarning);
        Assert.Equal(LibMpvLibraryLoader.X86_64V3FileName, result.Diagnostics.LoadedFile);
    }

    [Fact]
    public void LoaderAlwaysUsesTheApplicationBaseDirectory()
    {
        string packageDirectory = Path.Combine(Path.GetTempPath(), "mpvnet package directory");
        FakeNativeLibraryLoader nativeLoader = new((LibMpvLibraryLoader.X86_64V3FileName, true, ""));

        LibMpvLoadResult result = CreateLoader(nativeLoader, appBaseDirectory: packageDirectory).Load();

        Assert.Equal(
            Path.Combine(Path.GetFullPath(packageDirectory), LibMpvLibraryLoader.X86_64V3FileName),
            result.Diagnostics.LoadedPath);
        Assert.DoesNotContain(Environment.CurrentDirectory, result.Diagnostics.LoadedPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnrelatedNativeLibraryIsNotIntercepted()
    {
        IntPtr result = RuntimeComponents.ResolveNativeLibrary(
            "unrelated-native-library.dll",
            typeof(RuntimeComponents).Assembly,
            null);

        Assert.Equal(IntPtr.Zero, result);
    }

    [Fact]
    public void ResolverRegistrationIsIdempotent()
    {
        RuntimeComponents.RegisterNativeResolver();
        RuntimeComponents.RegisterNativeResolver();
    }

    [Theory]
    [InlineData(nameof(CpuFeatureSet.Sse3))]
    [InlineData(nameof(CpuFeatureSet.Ssse3))]
    [InlineData(nameof(CpuFeatureSet.Sse41))]
    [InlineData(nameof(CpuFeatureSet.Sse42))]
    [InlineData(nameof(CpuFeatureSet.Popcnt))]
    [InlineData(nameof(CpuFeatureSet.Avx))]
    [InlineData(nameof(CpuFeatureSet.Avx2))]
    [InlineData(nameof(CpuFeatureSet.F16c))]
    [InlineData(nameof(CpuFeatureSet.Fma))]
    [InlineData(nameof(CpuFeatureSet.Bmi1))]
    [InlineData(nameof(CpuFeatureSet.Bmi2))]
    [InlineData(nameof(CpuFeatureSet.Lzcnt))]
    [InlineData(nameof(CpuFeatureSet.Movbe))]
    public void EveryRequiredCpuFeatureIsRequiredForV3(string missingFeature)
    {
        CpuFeatureSet features = WithFeatureDisabled(missingFeature);

        Assert.False(features.SupportsX86_64V3);
    }

    [Fact]
    public void AllRequiredCpuFeaturesEnableV3()
    {
        Assert.True(new CpuFeatureSet().SupportsX86_64V3);
    }

    static LibMpvLibraryLoader CreateLoader(
        FakeNativeLibraryLoader nativeLoader,
        CpuFeatureSet? features = null,
        bool is64BitProcess = true,
        bool isX64Architecture = true,
        string? preference = "auto",
        string? appBaseDirectory = null)
    {
        return new(
            new FakeCpuFeatureProvider(is64BitProcess, isX64Architecture, features ?? new CpuFeatureSet()),
            nativeLoader,
            () => appBaseDirectory ?? PackageDirectory,
            () => preference);
    }

    static CpuFeatureSet WithFeatureDisabled(string name) => name switch
    {
        nameof(CpuFeatureSet.Sse3) => new CpuFeatureSet { Sse3 = false },
        nameof(CpuFeatureSet.Ssse3) => new CpuFeatureSet { Ssse3 = false },
        nameof(CpuFeatureSet.Sse41) => new CpuFeatureSet { Sse41 = false },
        nameof(CpuFeatureSet.Sse42) => new CpuFeatureSet { Sse42 = false },
        nameof(CpuFeatureSet.Popcnt) => new CpuFeatureSet { Popcnt = false },
        nameof(CpuFeatureSet.Avx) => new CpuFeatureSet { Avx = false },
        nameof(CpuFeatureSet.Avx2) => new CpuFeatureSet { Avx2 = false },
        nameof(CpuFeatureSet.F16c) => new CpuFeatureSet { F16c = false },
        nameof(CpuFeatureSet.Fma) => new CpuFeatureSet { Fma = false },
        nameof(CpuFeatureSet.Bmi1) => new CpuFeatureSet { Bmi1 = false },
        nameof(CpuFeatureSet.Bmi2) => new CpuFeatureSet { Bmi2 = false },
        nameof(CpuFeatureSet.Lzcnt) => new CpuFeatureSet { Lzcnt = false },
        nameof(CpuFeatureSet.Movbe) => new CpuFeatureSet { Movbe = false },
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown CPU feature")
    };

    sealed class FakeCpuFeatureProvider : ICpuFeatureProvider
    {
        public FakeCpuFeatureProvider(bool is64BitProcess, bool isX64Architecture, CpuFeatureSet features)
        {
            Is64BitProcess = is64BitProcess;
            IsX64Architecture = isX64Architecture;
            Features = features;
        }

        public bool Is64BitProcess { get; }
        public bool IsX64Architecture { get; }
        public CpuFeatureSet Features { get; }
    }

    sealed class FakeNativeLibraryLoader : INativeLibraryLoader
    {
        readonly Dictionary<string, (bool Succeeds, string FailureReason)> _outcomes =
            new(StringComparer.OrdinalIgnoreCase);

        public FakeNativeLibraryLoader(params (string FileName, bool Succeeds, string FailureReason)[] outcomes)
        {
            foreach ((string fileName, bool succeeds, string failureReason) in outcomes)
                _outcomes[fileName] = (succeeds, failureReason);
        }

        public List<string> AttemptedFiles { get; } = [];

        public bool TryLoad(string libraryPath, out IntPtr handle, out string failureReason)
        {
            string fileName = Path.GetFileName(libraryPath);
            AttemptedFiles.Add(fileName);
            if (_outcomes.TryGetValue(fileName, out (bool Succeeds, string FailureReason) outcome) && outcome.Succeeds)
            {
                handle = new IntPtr(1);
                failureReason = "";
                return true;
            }

            handle = IntPtr.Zero;
            failureReason = _outcomes.TryGetValue(fileName, out outcome)
                ? outcome.FailureReason
                : "simulated missing file";
            return false;
        }
    }
}
