
using System.Linq;
using System.Windows;
using MpvNet;

namespace MpvNet.Windows.WPF;

public class WpfApplication
{
    static bool _initialized;

    public static void Init()
    {
        if (_initialized)
            return;

        if (Application.Current == null)
            new Application();

        Application.Current!.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Application.Current!.DispatcherUnhandledException += (sender, e) => Terminal.WriteError(e.Exception);

        Translator.Current?.Gettext("");

        var resourcesSource = "mpvnet;component/WPF/Resources.xaml";
        if (!Application.Current.Resources.MergedDictionaries.Any(rd => rd.Source?.OriginalString == resourcesSource))
        {
            Application.Current.Resources.MergedDictionaries.Add(
                Application.LoadComponent(new Uri(resourcesSource, UriKind.Relative)) as ResourceDictionary);
        }

        _initialized = true;
    }
}
