
using System.Runtime.InteropServices;
using System.Text;

namespace MpvNet;

/// <summary>
/// Provides the application directories used by the player.
/// </summary>
public static class AppPaths
{
    public static string Startup { get; } = EnsureTrailingSeparator(Path.GetFullPath(AppContext.BaseDirectory));
    public static string LaunchDirectory { get; } = Path.GetFullPath(Environment.CurrentDirectory);
    public static string? PackageFamilyName { get; } = GetCurrentPackageFamilyName();
    public static bool IsPackaged => PackageFamilyName is not null;
    public static string LocalAppData { get; } = EnsureTrailingSeparator(ResolveLocalAppData(
        Startup, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetEnvironmentVariable("MPVNET_LOCAL_APPDATA"), PackageFamilyName));

    public static string PortableConfig { get; } = Path.Combine(Startup, "portable_config");
    public static string Locale { get; } = Path.Combine(Startup, "Locale");
    public static string LocalRoot { get; } = Path.Combine(LocalAppData, "mpv.net");
    public static string DefaultConfig { get; } = LocalRoot;
    public static string Cache { get; } = Path.Combine(LocalRoot, "Cache");
    public static string Components { get; } = Path.Combine(LocalRoot, "Component");
    public static string Logs { get; } = Path.Combine(LocalRoot, "Logs");
    public static string Temp { get; } = Path.Combine(LocalRoot, "Temp");
    public static string ComponentTemp { get; } = Path.Combine(Components, "staging");

    public static void EnsureLocalDirectories()
    {
        Directory.CreateDirectory(LocalRoot);
        Directory.CreateDirectory(Cache);
        Directory.CreateDirectory(Components);
        if (Log.IsEnabled)
            Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Temp);
        Directory.CreateDirectory(ComponentTemp);
    }

    internal static string ResolveLocalAppData(string startup, string localAppData, string? overridePath, string? packageFamilyName)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
            return ResolveDirectory(overridePath, startup);
        localAppData = ResolveDirectory(localAppData, startup);
        string suffix = Path.Combine("Packages", packageFamilyName ?? "", "LocalCache", "Local");
        if (packageFamilyName is not null && localAppData.EndsWith(
            Path.DirectorySeparatorChar + suffix, StringComparison.OrdinalIgnoreCase))
            return localAppData;
        return packageFamilyName is null
            ? localAppData
            : Path.Combine(localAppData, suffix);
    }

    internal static string ResolveDirectory(string value, string relativeTo) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(
            Environment.ExpandEnvironmentVariables(value.Trim().Trim('"')), relativeTo));

    internal static (string Path, string Source) ResolveConfigurationDirectory(
        string startup, string defaultConfig, string launchDirectory, string? home,
        string? commandLineDirectory, bool packaged)
    {
        if (commandLineDirectory is not null)
            return (WithTrailingSeparator(ResolveDirectory(commandLineDirectory, launchDirectory)), "command-line");
        if (!string.IsNullOrWhiteSpace(home))
        {
            try
            {
                string candidate = ResolveDirectory(home, startup);
                if (Directory.Exists(candidate))
                    return (WithTrailingSeparator(candidate), "MPVNET_HOME");
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // An invalid optional MPVNET_HOME has historically fallen back.
            }
        }
        string portable = Path.Combine(startup, "portable_config");
        // The MSIX installation is read-only, even if portable_config was
        // accidentally included in its payload.
        if (!packaged && Directory.Exists(portable))
            return (WithTrailingSeparator(portable), "portable_config");
        return (WithTrailingSeparator(defaultConfig), packaged ? "package-local-data" : "local-data");
    }

    internal static (string Path, string Source) ResolveConfigurationDirectory(IEnumerable<StringPair> arguments) =>
        ResolveConfigurationDirectory(Startup, DefaultConfig, LaunchDirectory,
            Environment.GetEnvironmentVariable("MPVNET_HOME"),
            arguments.LastOrDefault(argument => argument.Name == "config-dir")?.Value, IsPackaged);

    public static string DiagnosePaths() => DiagnosePaths(Environment.GetCommandLineArgs().Skip(1));

    public static string DiagnosePaths(IEnumerable<string> arguments) =>
        DiagnosePaths(CommandLine.ParseArguments(arguments, log: false));

    internal static string DiagnosePaths(IEnumerable<StringPair> arguments)
    {
        var config = ResolveConfigurationDirectory(arguments);
        return string.Join(Environment.NewLine, new[]
        {
            "Application paths:",
            "Expected application paths before mpv initialization; explicit native file/cache overrides are applied by mpv.",
            $"package-family={PackageFamilyName ?? "<unpackaged>"}",
            $"installation={Startup}",
            $"launch-directory={LaunchDirectory}",
            $"configuration={config.Path}; source={config.Source}; exists={Directory.Exists(config.Path)}",
            $"mpv-conf={Path.Combine(config.Path, "mpv.conf")}",
            $"mpvnet-conf={Path.Combine(config.Path, "mpvnet.conf")}",
            $"frontend-input-conf={Path.Combine(config.Path, "input.conf")}",
            $"menu={Path.Combine(config.Path, "menu.conf")}",
            $"global-input-conf={Path.Combine(config.Path, "global-input.conf")}",
            $"settings={Path.Combine(config.Path, "settings.xml")}",
            $"theme={Path.Combine(config.Path, "theme.conf")}",
            $"user-scripts={Path.Combine(config.Path, "scripts")}",
            $"script-options={Path.Combine(config.Path, "script-opts")}",
            $"extensions={Path.Combine(config.Path, "extensions")}",
            $"local-app-data={LocalAppData}",
            $"cache={Cache}",
            $"components={Components}",
            $"components-current={Path.Combine(Components, "current")}",
            $"components-previous={Path.Combine(Components, "previous")}",
            $"components-staging={ComponentTemp}",
            $"temp={Temp}",
            $"logs={Logs}",
            $"native-libraries={Startup}",
            $"libmpv-normal={Path.Combine(Startup, "libmpv-2.dll")}",
            $"libmpv-v3={Path.Combine(Startup, "libmpv-2-v3.dll")}",
            $"mediainfo={Path.Combine(Startup, "MediaInfo.dll")}",
            $"locale={Locale}",
            $"scripts={Path.Combine(Startup, "Scripts")}",
            "Writable status is not probed; this report does not create directories."
        });
    }

    static string? GetCurrentPackageFamilyName()
    {
        uint length = 0;
        int result = GetCurrentPackageFamilyNameNative(ref length, null);
        if (result == 15700 /* APPMODEL_ERROR_NO_PACKAGE */)
            return null;
        if (result != 122 /* ERROR_INSUFFICIENT_BUFFER */ || length == 0)
            return null;

        StringBuilder packageFamilyName = new((int)length);
        result = GetCurrentPackageFamilyNameNative(ref length, packageFamilyName);
        return result == 0 && packageFamilyName.Length > 0 ? packageFamilyName.ToString() : null;
    }

    [DllImport("kernel32.dll", EntryPoint = "GetCurrentPackageFamilyName", CharSet = CharSet.Unicode)]
    static extern int GetCurrentPackageFamilyNameNative(ref uint packageFamilyNameLength, [Out] StringBuilder? packageFamilyName);

    static string EnsureTrailingSeparator(string path) =>
        Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;

    public static string WithTrailingSeparator(string path) => EnsureTrailingSeparator(path);
}

/// <summary>
/// Compatibility facade for the former directory provider name.
/// </summary>
[Obsolete($"Use {nameof(AppPaths)} instead.")]
public static class Folder
{
    public static string Startup => AppPaths.Startup;
    public static string AppData => AppPaths.LocalAppData;
    public static string LocalAppData => AppPaths.LocalAppData;
}
