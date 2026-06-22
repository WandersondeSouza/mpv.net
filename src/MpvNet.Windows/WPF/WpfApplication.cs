
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

        Application application = Application.Current ?? new Application();

        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        application.DispatcherUnhandledException += (sender, e) =>
        {
            Log.Error(e.Exception, "Unhandled WPF dispatcher exception.");
            Terminal.WriteError(e.Exception);
        };

        TranslationProvider.Current?.Gettext("");

        var resourcesSource = "mpvnet;component/WPF/Resources.xaml";
        if (!application.Resources.MergedDictionaries.Any(rd => rd.Source?.OriginalString == resourcesSource))
        {
            if (Application.LoadComponent(new Uri(resourcesSource, UriKind.Relative)) is ResourceDictionary resources)
                application.Resources.MergedDictionaries.Add(resources);
        }

        _initialized = true;
    }
}
