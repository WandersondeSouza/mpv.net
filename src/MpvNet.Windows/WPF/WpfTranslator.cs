
using NGettext.Wpf;

using System.Globalization;
using System.Threading;

namespace MpvNet.Windows.WPF;

public class WpfTranslator : ITranslator
{
    string _localizerLangauge = "";

    public static event EventHandler<CultureInfo>? LanguageChanged;

    public string Gettext(string msgId)
    {
        InitNGettextWpf();
        return Translation._(msgId);
    }

    public string GetParticularString(string context, string text)
    {
        InitNGettextWpf();
        return Translation.GetParticularString(context, text);
    }

    void InitNGettextWpf()
    {
        CultureInfo culture = GetCulture(App.Language);
        ApplyThreadCulture(culture);

        if (Translation.Localizer == null)
        {
            CompositionRoot.Compose("mpvnet", culture, Folder.Startup + "Locale");
            _localizerLangauge = App.Language;
            return;
        }

        if (_localizerLangauge != App.Language)
        {
            Translation.Localizer.CultureTracker.CurrentCulture = culture;
            _localizerLangauge = App.Language;
            LanguageChanged?.Invoke(this, culture);
        }
    }

    static void ApplyThreadCulture(CultureInfo culture)
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }

    public static string GetEffectiveLanguage(string name)
    {
        return LocalizationService.ResolveMpvNetLanguage(name);
    }

    public static string GetEffectiveLanguageFromAlang(string? alang)
    {
        return LocalizationService.ResolveFromMpvLanguageList(alang);
    }

    CultureInfo GetCulture(string name)
    {
        return LocalizationService.GetCulture(name);
    }
}
