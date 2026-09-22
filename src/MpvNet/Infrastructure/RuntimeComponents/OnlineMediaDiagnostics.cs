using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MpvNet;

internal sealed record ExecutableProbeResult(
    bool Started,
    int? ExitCode,
    string Output,
    string? Failure)
{
    public bool Succeeded => Started && ExitCode == 0;
}

internal sealed record JavaScriptRuntimeCapability(
    string Name,
    string Command,
    string? Version,
    bool Available,
    bool Compatible,
    bool EnabledByDefault);

internal static partial class OnlineMediaDiagnostics
{
    static readonly (string Name, string Command, string Argument, Version Minimum, bool EnabledByDefault)[] JavaScriptRuntimes =
    [
        ("Deno", "deno", "--version", new Version(2, 3), true),
        ("Node.js", "node", "--version", new Version(22, 0), false),
        ("QuickJS", "qjs", "--version", new Version(2023, 12, 9), false)
    ];

    public static string BuildReport(ComponentResolutionResult ytDlp, ComponentResolutionResult ffmpeg)
    {
        ExecutableProbeResult ytDlpVersion = ProbeResolved(ytDlp, "--version");
        ExecutableProbeResult ffmpegVersion = ProbeResolved(ffmpeg, "-version");
        JavaScriptRuntimeCapability[] runtimes = JavaScriptRuntimes.Select(ProbeJavaScriptRuntime).ToArray();
        JavaScriptRuntimeCapability? preferredRuntime = SelectPreferredRuntime(runtimes);
        ExecutableProbeResult impersonation = ProbeResolved(ytDlp, "--list-impersonate-targets");
        bool impersonationAvailable = impersonation.Succeeded &&
            impersonation.Output.Contains("curl_cffi", StringComparison.OrdinalIgnoreCase);

        string ejsCapability = preferredRuntime is null
            ? "limited: no compatible JavaScript runtime detected"
            : $"available through yt-dlp with {preferredRuntime.Name} {preferredRuntime.Version}";

        return string.Join(Environment.NewLine, new string[]
        {
            "Online media capability chain:",
            $"yt-dlp executable: version={FirstLineOrUnavailable(ytDlpVersion)}; path={ytDlp.ResolvedPath ?? "<unavailable>"}; source={ytDlp.Source}",
            $"FFmpeg executable: version={FirstLineOrUnavailable(ffmpegVersion)}; path={ffmpeg.ResolvedPath ?? "<unavailable>"}; source={ffmpeg.Source}",
            $"JavaScript runtimes: {string.Join(", ", runtimes.Select(FormatRuntime))}",
            $"YouTube EJS challenge support: {ejsCapability}; remote components are not enabled by MPV.NET",
            $"Browser impersonation: {(impersonationAvailable ? "available (optional curl_cffi targets detected)" : "unavailable or not reported")}",
            "PO Token: external provider/configuration only; tokens, cookies, authorization headers and signed URL queries are not collected by this diagnostic"
        });
    }

    internal static JavaScriptRuntimeCapability? SelectPreferredRuntime(IEnumerable<JavaScriptRuntimeCapability> runtimes)
    {
        JavaScriptRuntimeCapability[] candidates = runtimes as JavaScriptRuntimeCapability[] ?? runtimes.ToArray();
        return candidates.FirstOrDefault(runtime => runtime.Available && runtime.Compatible && runtime.EnabledByDefault) ??
            candidates.FirstOrDefault(runtime => runtime.Available && runtime.Compatible);
    }

    internal static bool IsCompatibleJavaScriptRuntime(string name, string? version)
    {
        var runtime = JavaScriptRuntimes.FirstOrDefault(candidate =>
            candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return runtime != default && TryParseVersion(version, out Version? parsed) && parsed >= runtime.Minimum;
    }

    internal static bool TryParseVersion(string? value, out Version? version)
    {
        version = null;
        Match match = VersionPattern().Match(value ?? "");
        return match.Success && Version.TryParse(match.Value.Replace('-', '.'), out version);
    }

    static JavaScriptRuntimeCapability ProbeJavaScriptRuntime(
        (string Name, string Command, string Argument, Version Minimum, bool EnabledByDefault) runtime)
    {
        ExecutableProbeResult probe = Probe(runtime.Command, runtime.Argument);
        string? version = TryParseVersion(probe.Output, out Version? parsed) ? parsed?.ToString() : null;
        return new(runtime.Name, runtime.Command, version, probe.Succeeded, probe.Succeeded && parsed >= runtime.Minimum,
            runtime.EnabledByDefault);
    }

    static ExecutableProbeResult ProbeResolved(ComponentResolutionResult component, params string[] arguments) =>
        component is { IsValid: true, ResolvedPath: not null }
            ? Probe(component.ResolvedPath, arguments)
            : new(false, null, "", component.DiagnosticMessage ?? "component unavailable");

    static ExecutableProbeResult Probe(string executable, params string[] arguments)
    {
        try
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            foreach (string argument in arguments)
                process.StartInfo.ArgumentList.Add(argument);

            if (!process.Start())
                return new(false, null, "", "process did not start");

            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

            try
            {
                process.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                string failure = "probe timed out after 5 seconds";
                try
                {
                    if (!process.HasExited)
                        process.Kill(true);
                    process.WaitForExit();
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    failure += $"; process termination failed: {ex.Message}";
                }

                return new(true, null, "", failure);
            }

            string output = string.Join(Environment.NewLine, new[]
            {
                stdout.GetAwaiter().GetResult(),
                stderr.GetAwaiter().GetResult()
            }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
            return new(true, process.ExitCode, output, process.ExitCode == 0 ? null : "probe exited with an error");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            return new(false, null, "", ex.Message);
        }
    }

    static string FirstLineOrUnavailable(ExecutableProbeResult probe) =>
        probe.Succeeded
            ? probe.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "<unknown>"
            : $"<unavailable: {probe.Failure ?? "probe failed"}>";

    static string FormatRuntime(JavaScriptRuntimeCapability runtime)
    {
        if (!runtime.Available)
            return $"{runtime.Name}=not found";

        string mode = runtime.EnabledByDefault ? "default" : "requires explicit yt-dlp configuration";
        return $"{runtime.Name}={runtime.Version ?? "unknown"} ({(runtime.Compatible ? "compatible" : "unsupported version")}, {mode})";
    }

    [GeneratedRegex(@"\d+(?:[.-]\d+){1,3}", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
