
using MpvNet.Extensions;

namespace MpvNet;

/// <summary>
/// Provides the application directories used by the player.
/// </summary>
public static class AppPaths
{
    public static string Startup { get; } = AppContext.BaseDirectory.Separator();
    public static string AppData { get; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).Separator();
    public static string LocalAppData { get; } = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).Separator();
}

/// <summary>
/// Compatibility facade for the former directory provider name.
/// </summary>
[Obsolete($"Use {nameof(AppPaths)} instead.")]
public static class Folder
{
    public static string Startup => AppPaths.Startup;
    public static string AppData => AppPaths.AppData;
    public static string LocalAppData => AppPaths.LocalAppData;
}
