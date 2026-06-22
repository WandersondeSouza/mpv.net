
using System.Reflection;

namespace MpvNet;

public static class AppInfo
{
    public static string Product => GetAssemblyAttribute<AssemblyProductAttribute>().Product;
    public static Version Version => GetApplicationAssembly().GetName().Version ?? new Version(0, 0);

    static T GetAssemblyAttribute<T>() where T : Attribute =>
        GetApplicationAssembly().GetCustomAttribute<T>()
        ?? throw new InvalidOperationException($"Assembly attribute not found: {typeof(T).Name}");

    static Assembly GetApplicationAssembly() =>
        Assembly.GetEntryAssembly() ?? typeof(AppInfo).Assembly;
}
