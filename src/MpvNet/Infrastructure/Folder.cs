
using System.Runtime.InteropServices;
using System.Text;

namespace MpvNet;

/// <summary>
/// Provides the application directories used by the player.
/// </summary>
public static class AppPaths
{
    public static string Startup { get; } = EnsureTrailingSeparator(AppContext.BaseDirectory);
    public static string LocalAppData { get; } = EnsureTrailingSeparator(ResolveLocalAppData());

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

    static string ResolveLocalAppData()
    {
        if (Environment.GetEnvironmentVariable("MPVNET_LOCAL_APPDATA") is { Length: > 0 } overridePath)
            return overridePath;

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string? packageFamilyName = GetCurrentPackageFamilyName();
        return packageFamilyName is null
            ? localAppData
            : Path.Combine(localAppData, "Packages", packageFamilyName, "LocalCache", "Local");
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
        Path.TrimEndingDirectorySeparator(path) + Path.DirectorySeparatorChar;

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
