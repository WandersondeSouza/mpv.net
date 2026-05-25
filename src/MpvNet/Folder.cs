
using MpvNet.Extensions;

namespace MpvNet;

public class Folder
{
    public static string Startup { get; } = AppContext.BaseDirectory.Separator;
    public static string AppData { get; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).Separator;
}
