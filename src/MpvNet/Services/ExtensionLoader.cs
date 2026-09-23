
using System.Reflection;

using MpvNet.Extensions;

namespace MpvNet;

/// <summary>
/// Discovers and loads managed MPV.NET extensions from configuration folders.
/// </summary>
public class ExtensionService
{
    public event Action<Exception>? UnhandledException;

    // Extensions are loaded into AssemblyLoadContext.Default and intentionally remain
    // alive for the process lifetime. Hot reload/unload is not part of this service.
    readonly List<object> _refs = [];
    internal int LoadedExtensionCount => _refs.Count;

    void LoadDll(string path)
    {
        if (!File.Exists(path))
            return;

        try
        {
            Assembly asm = Assembly.LoadFile(path);
            Type[] exportedTypes;
            try
            {
                exportedTypes = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                Exception[] loaderExceptions = ex.LoaderExceptions.OfType<Exception>().ToArray();
                throw new InvalidOperationException(
                    $"Extension assembly '{Path.GetFileName(path)}' has unavailable dependencies.",
                    new AggregateException(loaderExceptions));
            }

            Type[] extensionTypes = exportedTypes
                .Where(type => typeof(IExtension).IsAssignableFrom(type) && type is { IsClass: true, IsAbstract: false })
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();
            if (extensionTypes.Length == 0)
                throw new InvalidDataException(
                    $"Extension assembly '{Path.GetFileName(path)}' does not contain a concrete {nameof(IExtension)} type.");

            foreach (Type type in extensionTypes)
            {
                object instance = Activator.CreateInstance(type)
                    ?? throw new InvalidOperationException($"Extension type '{type.FullName}' could not be created.");
                _refs.Add(instance);
            }
        }
        catch (Exception ex)
        {
            try
            {
                UnhandledException?.Invoke(ex);
            }
            catch (Exception handlerException)
            {
                Terminal.WriteError(handlerException);
            }
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
