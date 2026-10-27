
using NGettext.Wpf;

using System.Globalization;

namespace MpvNet.Windows.WPF;

public class WpfTranslator : ITranslator
{
    string _localizerLangauge = "";

    public static event EventHandler<CultureInfo>? LanguageChanged;

    public string Gettext(string msgId)
    {
        InitNGettextWpf();
        // ITranslator has an explicit context API. A literal '|' in an mpv
        // option such as <N|inf|no> must not be parsed as context syntax.
        return Translation.Localizer!.Catalog.GetString(msgId);
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
            CompositionRoot.Compose("mpvnet", culture, AppPaths.Locale);
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
        // Gettext selects text; regional numbers/calendar remain the user's.
        CultureInfo.CurrentUICulture = culture;
        InterfaceLayout.ApplyCulture(culture);
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
