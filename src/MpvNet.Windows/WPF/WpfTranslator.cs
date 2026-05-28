
using NGettext.Wpf;

using System.Globalization;

using System.Threading;

namespace MpvNet.Windows.WPF;

public class WpfTranslator : ITranslator
{
    static readonly CultureInfo SystemUICulture = CultureInfo.CurrentUICulture;

    string _localizerLangauge = "";

    public static event EventHandler<CultureInfo>? LanguageChanged;

    static Language[] Languages { get; } = new Language[] {
        new("bulgarian", "bg", "bg", "bul", "bulgarian"),
        new("chinese-china", "zh-CN", "zh", "zh-cn", "chi", "zho", "chinese"),
        new("english", "en", "en", "eng", "english"),
        new("spanish", "es", "es", "spa", "spanish"),
        new("french", "fr", "fr", "fra", "fre", "french"),
        new("german", "de", "de", "deu", "ger", "german"),
        new("japanese", "ja", "ja", "jpn", "japanese"),
        new("korean", "ko", "ko", "kor", "korean"),
        new("polish", "pl", "pl", "pol", "polish"),
        new("portuguese-brazil", "pt-BR", "pt", "pt-br", "por-br", "por", "portuguese", "brazilian portuguese"),
        new("portuguese-portugal", "pt-PT", "pt", "pt-pt", "por-pt", "portuguese-portugal", "european portuguese"),
        new("russian", "ru", "ru", "rus", "russian"),
        new("turkish", "tr", "tr", "tur", "turkish"),
    };

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
        if (name != "system")
            return GetKnownLanguage(name) ?? "english";

        string systemLanguage = GetSystemLanguage();
        return GetKnownLanguage(systemLanguage) ?? "english";
    }

    public static string GetEffectiveLanguageFromAlang(string? alang)
    {
        foreach (string item in (alang ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string? language = GetKnownLanguage(item);

            if (language != null)
                return language;
        }

        return GetEffectiveLanguage("system");
    }

    static string? GetKnownLanguage(string name)
    {
        string normalized = NormalizeLanguageName(name);

        foreach (Language lang in Languages)
            if (lang.Matches(normalized))
                return lang.MpvNetName;

        return null;
    }

    static string NormalizeLanguageName(string? name) =>
        (name ?? "").Trim().Replace('_', '-').ToLowerInvariant();

    static string GetSystemLanguage()
    {
        string twoLetterName = SystemUICulture.TwoLetterISOLanguageName;

        if (twoLetterName == "zh")
            return "chinese-china";  // Chinese (Simplified)

        if (SystemUICulture.Name == "pt-BR")
            return "portuguese-brazil";

        if (SystemUICulture.Name == "pt-PT")
            return "portuguese-portugal";

        return new CultureInfo(twoLetterName).EnglishName.ToLowerInvariant();
    }

    CultureInfo GetCulture(string name)
    {
        name = GetEffectiveLanguage(name);

        foreach (Language lang in Languages)
            if (lang.MpvNetName == name)
                return new CultureInfo(lang.CultureInfoName.Replace('-', '_'));

        return new CultureInfo("en");
    }

    class Language
    {
        public string MpvNetName { get; }
        public string CultureInfoName { get; }
        public string TwoLetterName { get; }
        public string[] Aliases { get; }

        public Language(string mpvNetName, string cultureInfoName, string twoLetterName, params string[] aliases)
        {
            MpvNetName = mpvNetName;
            CultureInfoName = cultureInfoName;
            TwoLetterName = twoLetterName;
            Aliases = aliases
                .Concat(new[] { mpvNetName, cultureInfoName, twoLetterName })
                .Select(NormalizeLanguageName)
                .Distinct()
                .ToArray();
        }

        public bool Matches(string normalizedName) => Aliases.Contains(normalizedName);
    }
}
