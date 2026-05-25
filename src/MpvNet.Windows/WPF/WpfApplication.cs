
using System.Windows;
using MpvNet;

namespace MpvNet.Windows.WPF;

public class WpfApplication
{
    public static void Init()
    {
        new Application();

        Application.Current!.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Application.Current!.DispatcherUnhandledException += (sender, e) => Terminal.WriteError(e.Exception);

        Translator.Current?.Gettext("");

        Application.Current?.Resources.MergedDictionaries.Add(
            Application.LoadComponent(new Uri("mpvnet;component/WPF/Resources.xaml",
                UriKind.Relative)) as ResourceDictionary);
    }
}
