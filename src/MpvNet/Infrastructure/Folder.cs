
namespace MpvNet;

/// <summary>
/// Provides the application directories used by the player.
/// </summary>
public static class AppPaths
{
    public static string Startup { get; } = EnsureTrailingSeparator(AppContext.BaseDirectory);
    public static string LocalAppData { get; } = EnsureTrailingSeparator(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static string PortableConfig { get; } = Path.Combine(Startup, "portable_config");
    public static string Locale { get; } = Path.Combine(Startup, "Locale");
    public static string LocalRoot { get; } = Path.Combine(LocalAppData, "mpv.net");
    public static string DefaultConfig { get; } = LocalRoot;
    public static string Cache { get; } = Path.Combine(LocalRoot, "Cache");
    public static string Components { get; } = Path.Combine(LocalRoot, "Component");
    public static string Logs { get; } = Path.Combine(LocalRoot, "Logs");
    public static string Temp { get; } = Path.Combine(LocalRoot, "Temp");
    public static string ComponentTemp { get; } = Path.Combine(Temp, "RuntimeComponents");

    public static void EnsureLocalDirectories()
    {
        Directory.CreateDirectory(LocalRoot);
        Directory.CreateDirectory(Cache);
        Directory.CreateDirectory(Components);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Temp);
        Directory.CreateDirectory(ComponentTemp);
    }

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
