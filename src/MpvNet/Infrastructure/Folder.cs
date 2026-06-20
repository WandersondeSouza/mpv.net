
using MpvNet.Extensions;

namespace MpvNet;

public class Folder
{
    public static string Startup { get; } = AppContext.BaseDirectory.Separator();
    public static string AppData { get; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).Separator();
    public static string LocalAppData { get; } = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).Separator();
}
