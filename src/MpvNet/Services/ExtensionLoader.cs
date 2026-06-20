
using System.Reflection;

using MpvNet.Extensions;

namespace MpvNet;

/// <summary>
/// Discovers and loads managed MPV.NET extensions from configuration folders.
/// </summary>
public class ExtensionService
{
    public event Action<Exception>? UnhandledException;

    readonly List<object?> _refs = [];

    void LoadDll(string path)
    {
        if (!File.Exists(path))
            return;

        try
        {
            Assembly asm = Assembly.LoadFile(path);
            var type = asm.GetTypes().Where(typeof(IExtension).IsAssignableFrom).First();
            _refs.Add(Activator.CreateInstance(type));
        }
        catch (Exception ex)
        {
            UnhandledException?.Invoke(ex);
        }
    }

    public void LoadFolder(string folderPath)
    {
        if (Directory.Exists(folderPath))
        {
            foreach (string directory in Directory.GetDirectories(folderPath))
            {
                LoadDll(directory.Separator() + Path.GetFileName(directory) + ".dll");
            }
        }
    }
}

/// <summary>
/// Compatibility facade for the former extension service name.
/// </summary>
[Obsolete($"Use {nameof(ExtensionService)} instead.")]
public class ExtensionLoader : ExtensionService
{
}

/// <summary>
/// Marker contract implemented by managed MPV.NET extensions.
/// </summary>
public interface IExtension
{
}
