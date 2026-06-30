using System.Globalization;
using System.Text;

namespace MpvNet;

public sealed record LanguageDefinition(
    string MpvNetName,
    string CultureName,
    string LocaleFolder,
    bool HasGettextCatalog,
    params string[] Aliases)
{
    public string NormalizedCultureName { get; } = LanguageNormalizer.Normalize(CultureName) ?? CultureName;

    public bool Matches(string? value)
    {
        string? normalized = LanguageNormalizer.Normalize(value);
        if (normalized == null)
            return false;

        return GetAliases().Any(alias => StringComparer.OrdinalIgnoreCase.Equals(alias, normalized));
    }

    IEnumerable<string> GetAliases()
    {
        yield return NormalizedCultureName;
        yield return MpvNetName;

        foreach (string alias in Aliases)
            yield return LanguageNormalizer.Normalize(alias) ?? LanguageNormalizer.NormalizeNameAlias(alias);
    }
}

public static class LanguageCatalog
{
    public static readonly LanguageDefinition DefaultLanguage = new("english", "en", "", false,
        "eng", "english");

    public static IReadOnlyList<LanguageDefinition> InterfaceLanguages { get; } =
    [
        new("bulgarian", "bg", "bg", true, "bul", "bulgarian"),
        new("chinese-china", "zh-CN", "zh_CN", true, "chi", "zho", "zh", "zh-cn", "zh_cn", "chinese", "simplified chinese"),
        DefaultLanguage,
        new("spanish", "es", "es", true, "spa", "spanish", "español"),
        new("french", "fr", "fr", true, "fra", "fre", "french"),
        new("german", "de", "de", true, "deu", "ger", "german"),
        new("italian", "it", "it", true, "ita", "italian"),
        new("japanese", "ja", "ja", true, "jpn", "japanese"),
        new("korean", "ko", "ko", true, "kor", "korean"),
        new("polish", "pl", "pl", true, "pol", "polish"),
        new("portuguese-brazil", "pt-BR", "pt_BR", true, "por-br", "pt-br", "pt_br", "brazilian portuguese", "português do brasil"),
        new("portuguese-portugal", "pt-PT", "pt_PT", true, "por-pt", "pt-pt", "pt_pt", "portuguese portugal", "portuguese-portugal", "european portuguese", "português de portugal"),
        new("russian", "ru", "ru", true, "rus", "russian"),
        new("turkish", "tr", "tr", true, "tur", "turkish"),
    ];

    public static IReadOnlyCollection<string> InterfaceCultureNames { get; } =
        InterfaceLanguages.Select(language => language.NormalizedCultureName).ToArray();

    public static IReadOnlyCollection<string> SupportedInterfaceCultureNames => InterfaceCultureNames;

    public static LanguageDefinition? FindInterfaceLanguage(string? value)
    {
        string? normalized = LanguageNormalizer.Normalize(value);
        string alias = LanguageNormalizer.NormalizeNameAlias(value);

        foreach (LanguageDefinition language in InterfaceLanguages)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(language.MpvNetName, value) ||
                StringComparer.OrdinalIgnoreCase.Equals(language.NormalizedCultureName, normalized) ||
                language.Matches(alias))
            {
                return language;
            }
        }

        return null;
    }
}

public static class LanguageNormalizer
{
    static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bul"] = "bg",
        ["bulgarian"] = "bg",
        ["eng"] = "en",
        ["english"] = "en",
        ["por"] = "pt",
        ["portuguese"] = "pt",
        ["portugues"] = "pt",
        ["brazilian portuguese"] = "pt-BR",
        ["portuguese brazil"] = "pt-BR",
        ["portugues do brasil"] = "pt-BR",
        ["portuguese portugal"] = "pt-PT",
        ["portuguese-portugal"] = "pt-PT",
        ["european portuguese"] = "pt-PT",
        ["portugues de portugal"] = "pt-PT",
        ["spa"] = "es",
        ["spanish"] = "es",
        ["espanol"] = "es",
        ["fra"] = "fr",
        ["fre"] = "fr",
        ["french"] = "fr",
        ["chi"] = "zh",
        ["zho"] = "zh",
        ["chinese"] = "zh",
        ["simplified chinese"] = "zh-CN",
        ["traditional chinese"] = "zh-TW",
        ["jpn"] = "ja",
        ["japanese"] = "ja",
        ["kor"] = "ko",
        ["korean"] = "ko",
        ["pol"] = "pl",
        ["polish"] = "pl",
        ["deu"] = "de",
        ["ger"] = "de",
        ["german"] = "de",
        ["ita"] = "it",
        ["italian"] = "it",
        ["rus"] = "ru",
        ["russian"] = "ru",
        ["tur"] = "tr",
        ["turkish"] = "tr",
    };

    public static string? Normalize(string? value)
    {
        string alias = NormalizeNameAlias(value);
        if (alias.Length == 0)
            return null;

        if (Aliases.TryGetValue(alias, out string? mapped))
            return mapped;

        string candidate = alias.Replace('_', '-');
        string[] parts = candidate.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return null;

        string language = parts[0];
        if (language.Length is < 2 or > 3 || !language.All(char.IsAsciiLetter))
            return null;

        if (language.Length == 3)
            language = ThreeLetterToTwoLetter(language);

        List<string> normalizedParts = [language.ToLowerInvariant()];

        foreach (string rawPart in parts.Skip(1))
        {
            if (rawPart.Length == 2 && rawPart.All(char.IsAsciiLetter))
                normalizedParts.Add(rawPart.ToUpperInvariant());
            else if (rawPart.Length == 4 && rawPart.All(char.IsAsciiLetter))
                normalizedParts.Add(char.ToUpperInvariant(rawPart[0]) + rawPart[1..].ToLowerInvariant());
            else if (rawPart.Length == 3 && rawPart.All(char.IsAsciiDigit))
                normalizedParts.Add(rawPart);
            else
                normalizedParts.Add(rawPart);
        }

        return string.Join("-", normalizedParts);
    }

    public static string NormalizeNameAlias(string? value)
    {
        string text = RemoveDiacritics((value ?? "").Trim())
            .Replace('_', '-')
            .ToLowerInvariant();

        text = string.Join(" ", text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));

        return text switch
        {
            "pt-br" => "pt-BR",
            "pt-pt" => "pt-PT",
            "en-us" => "en-US",
            "es-mx" => "es-MX",
            "fr-ca" => "fr-CA",
            "zh-cn" => "zh-CN",
            "zh-tw" => "zh-TW",
            _ => text,
        };
    }

    public static bool AreEquivalent(string? left, string? right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    public static string GetDisplayName(string? value)
    {
        string? normalized = Normalize(value);
        if (normalized == null)
            return value ?? "";

        try
        {
            return CultureInfo.GetCultureInfo(normalized).EnglishName;
        }
        catch (CultureNotFoundException)
        {
            return normalized;
        }
    }

    static string RemoveDiacritics(string text)
    {
        string normalized = text.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new();

        foreach (char c in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    static string ThreeLetterToTwoLetter(string language)
    {
        string mapped = language.ToLowerInvariant() switch
        {
            "eng" => "en",
            "por" => "pt",
            "spa" => "es",
            "fra" => "fr",
            "fre" => "fr",
            "chi" => "zh",
            "zho" => "zh",
            "jpn" => "ja",
            "kor" => "ko",
            "pol" => "pl",
            "deu" => "de",
            "ger" => "de",
            "ita" => "it",
            "rus" => "ru",
            "tur" => "tr",
            "bul" => "bg",
            _ => language.ToLowerInvariant(),
        };

        if (mapped.Length == 2)
            return mapped;

        foreach (CultureInfo culture in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
        {
            if (string.Equals(culture.ThreeLetterISOLanguageName, mapped, StringComparison.OrdinalIgnoreCase))
                return culture.TwoLetterISOLanguageName;
        }

        return mapped;
    }
}

public static class LocalizationCultureResolver
{
    public const string DefaultCultureName = "en";

    static readonly Dictionary<string, string> ExplicitCultureMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bg-BG"] = "bg",

        ["de-DE"] = "de",
        ["de-AT"] = "de",
        ["de-CH"] = "de",
        ["de-LI"] = "de",
        ["de-LU"] = "de",

        ["en-US"] = "en",
        ["en-GB"] = "en",
        ["en-AU"] = "en",
        ["en-CA"] = "en",
        ["en-NZ"] = "en",
        ["en-IE"] = "en",
        ["en-IN"] = "en",
        ["en-ZA"] = "en",
        ["en-SG"] = "en",
        ["en-HK"] = "en",

        ["es-ES"] = "es",
        ["es-MX"] = "es",
        ["es-AR"] = "es",
        ["es-CL"] = "es",
        ["es-CO"] = "es",
        ["es-PE"] = "es",
        ["es-UY"] = "es",
        ["es-VE"] = "es",
        ["es-EC"] = "es",
        ["es-BO"] = "es",
        ["es-PY"] = "es",
        ["es-CR"] = "es",
        ["es-DO"] = "es",
        ["es-GT"] = "es",
        ["es-HN"] = "es",
        ["es-NI"] = "es",
        ["es-PA"] = "es",
        ["es-PR"] = "es",
        ["es-SV"] = "es",
        ["es-US"] = "es",

        ["fr-FR"] = "fr",
        ["fr-CA"] = "fr",
        ["fr-BE"] = "fr",
        ["fr-CH"] = "fr",
        ["fr-LU"] = "fr",
        ["fr-MC"] = "fr",

        ["it-IT"] = "it",
        ["it-CH"] = "it",

        ["ja-JP"] = "ja",
        ["ko-KR"] = "ko",
        ["pl-PL"] = "pl",

        ["pt-BR"] = "pt-BR",
        ["pt-PT"] = "pt-PT",
        ["pt-AO"] = "pt-PT",
        ["pt-MZ"] = "pt-PT",
        ["pt-CV"] = "pt-PT",
        ["pt-GW"] = "pt-PT",
        ["pt-ST"] = "pt-PT",
        ["pt-TL"] = "pt-PT",
        ["pt-MO"] = "pt-PT",

        ["ru-RU"] = "ru",
        ["ru-BY"] = "ru",
        ["ru-KZ"] = "ru",
        ["ru-KG"] = "ru",
        ["ru-MD"] = "ru",

        ["tr-TR"] = "tr",
        ["tr-CY"] = "tr",

        ["zh-CN"] = "zh-CN",
        ["zh-SG"] = "zh-CN",
        ["zh-Hans"] = "zh-CN",
    };

    static readonly HashSet<string> GenericBaseFallbackCultures = new(StringComparer.OrdinalIgnoreCase)
    {
        "bg",
        "de",
        "en",
        "es",
        "fr",
        "it",
        "ja",
        "ko",
        "pl",
        "ru",
        "tr",
    };

    public static string ResolveSupportedCulture(
        string? requestedCulture,
        IEnumerable<string>? supportedCultures = null,
        string defaultCulture = DefaultCultureName)
    {
        string fallback = LanguageNormalizer.Normalize(defaultCulture) ?? DefaultCultureName;
        HashSet<string> supported = BuildSupportedSet(supportedCultures);
        string? resolved = ResolveKnownCulture(requestedCulture, supported);

        if (resolved != null)
            return resolved;

        return supported.Contains(fallback) ? fallback : DefaultCultureName;
    }

    public static string? ResolveKnownCulture(string? requestedCulture, IEnumerable<string>? supportedCultures = null)
    {
        HashSet<string> supported = BuildSupportedSet(supportedCultures);
        string? requested = LanguageNormalizer.Normalize(requestedCulture);

        if (requested == null)
            return null;

        if (supported.Contains(requested))
            return requested;

        if (ExplicitCultureMappings.TryGetValue(requested, out string? mapped) && supported.Contains(mapped))
            return mapped;

        string baseLanguage = requested.Split('-')[0];
        if (GenericBaseFallbackCultures.Contains(baseLanguage) && supported.Contains(baseLanguage))
            return baseLanguage;

        return null;
    }

    public static bool IsExplicitlyMappedVariant(string? requestedCulture)
    {
        string? requested = LanguageNormalizer.Normalize(requestedCulture);
        return requested != null && ExplicitCultureMappings.ContainsKey(requested);
    }

    static HashSet<string> BuildSupportedSet(IEnumerable<string>? supportedCultures)
    {
        IEnumerable<string> source = supportedCultures ?? LanguageCatalog.SupportedInterfaceCultureNames;
        return source
            .Select(LanguageNormalizer.Normalize)
            .Where(language => language != null)
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}

public static class LanguageFallbackResolver
{
    public static IReadOnlyList<string> GetFallbacks(
        string? requestedLanguage,
        IEnumerable<string>? availableLanguages = null,
        string defaultLanguage = "en")
    {
        string? requested = LanguageNormalizer.Normalize(requestedLanguage);
        string? normalizedDefault = string.IsNullOrWhiteSpace(defaultLanguage)
            ? null
            : LanguageNormalizer.Normalize(defaultLanguage) ?? "en";
        HashSet<string>? available = availableLanguages?
            .Select(LanguageNormalizer.Normalize)
            .Where(language => language != null)
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<string> candidates = [];
        string? resolvedSupportedCulture = LocalizationCultureResolver.ResolveKnownCulture(requested, available);

        if (resolvedSupportedCulture != null)
            Add(resolvedSupportedCulture);
        else
            Add(requested);

        if (requested != null)
        {
            if (!LocalizationCultureResolver.IsExplicitlyMappedVariant(requested))
                foreach (string variant in GetSafeVariants(requested))
                    Add(variant);

            string baseLanguage = requested.Split('-')[0];
            if (CanUseBaseFallback(requested) && !LocalizationCultureResolver.IsExplicitlyMappedVariant(requested))
                Add(baseLanguage);
        }

        Add(normalizedDefault);

        return candidates
            .Where(language => available == null || available.Contains(language))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        void Add(string? language)
        {
            if (!string.IsNullOrWhiteSpace(language))
                candidates.Add(language);
        }
    }

    static IEnumerable<string> GetSafeVariants(string requested) => requested switch
    {
        "es-MX" => ["es-419"],
        "zh-CN" => ["zh-Hans"],
        "zh-TW" => ["zh-Hant"],
        _ => [],
    };

    static bool CanUseBaseFallback(string requested)
    {
        if (!requested.Contains('-'))
            return false;

        if (requested.StartsWith("zh-", StringComparison.OrdinalIgnoreCase))
            return false;

        if (requested.StartsWith("sr-Cyrl", StringComparison.OrdinalIgnoreCase) ||
            requested.StartsWith("sr-Latn", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}

public static class LocalizationService
{
    public static string ResolveStartupLanguage(CultureInfo? systemCulture = null)
    {
        CultureInfo culture = systemCulture ?? CultureInfo.CurrentUICulture;
        return ResolveDefinition(culture.Name)?.MpvNetName ?? LanguageCatalog.DefaultLanguage.MpvNetName;
    }

    public static string ResolveMpvNetLanguage(string? configuredLanguage, CultureInfo? systemCulture = null)
    {
        return ResolveManualLanguage(configuredLanguage);
    }

    public static string ResolveFromMpvLanguageList(string? mpvLanguageList, CultureInfo? systemCulture = null)
    {
        foreach (string item in SplitLanguageList(mpvLanguageList))
        {
            LanguageDefinition? language = ResolveDefinition(item);
            if (language != null)
                return language.MpvNetName;
        }

        return ResolveStartupLanguage(systemCulture);
    }

    public static CultureInfo GetCulture(string? configuredLanguage)
    {
        string language = ResolveMpvNetLanguage(configuredLanguage);
        LanguageDefinition? definition = LanguageCatalog.FindInterfaceLanguage(language);
        return CultureInfo.GetCultureInfo(definition?.NormalizedCultureName ?? "en");
    }

    static string ResolveManualLanguage(string? configuredLanguage) =>
        ResolveDefinition(configuredLanguage)?.MpvNetName ?? LanguageCatalog.DefaultLanguage.MpvNetName;

    static LanguageDefinition? ResolveDefinition(string? value)
    {
        LanguageDefinition? configuredLanguage = LanguageCatalog.FindInterfaceLanguage(value);
        if (configuredLanguage != null)
            return configuredLanguage;

        string? normalized = LanguageNormalizer.Normalize(value);
        if (normalized == null)
            return null;

        foreach (string fallback in LanguageFallbackResolver.GetFallbacks(normalized, LanguageCatalog.InterfaceCultureNames))
        {
            LanguageDefinition? language = LanguageCatalog.InterfaceLanguages
                .FirstOrDefault(item => StringComparer.OrdinalIgnoreCase.Equals(item.NormalizedCultureName, fallback));
            if (language != null)
                return language;
        }

        return null;
    }

    static IEnumerable<string> SplitLanguageList(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public enum MediaLanguageSelectionMode
{
    Automatic,
    PreferredLanguage,
    Manual,
    Disabled
}

public static class MediaLanguageService
{
    public static IReadOnlyList<string> BuildMpvLanguagePriority(string? preferredLanguage, string? alternativeLanguage = null)
    {
        List<string> priority = [];
        AddFallbacks(preferredLanguage);
        AddFallbacks(alternativeLanguage);
        return priority.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        void AddFallbacks(string? value)
        {
            foreach (string language in LanguageFallbackResolver.GetFallbacks(value, defaultLanguage: ""))
                priority.Add(language);
        }
    }

    public static int? SelectPreferredTrack(
        IEnumerable<MediaTrack> tracks,
        string trackType,
        string? preferredLanguage,
        string? alternativeLanguage = null,
        MediaLanguageSelectionMode mode = MediaLanguageSelectionMode.PreferredLanguage,
        int? manualTrackId = null)
    {
        if (mode == MediaLanguageSelectionMode.Disabled)
            return null;

        if (mode == MediaLanguageSelectionMode.Manual)
            return manualTrackId;

        if (mode == MediaLanguageSelectionMode.Automatic || string.IsNullOrWhiteSpace(preferredLanguage))
            return null;

        MediaTrack[] typedTracks = tracks
            .Where(track => string.Equals(track.Type, trackType, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        HashSet<string> available = typedTracks
            .Select(track => LanguageNormalizer.Normalize(track.Language))
            .Where(language => language != null)
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string language in BuildMpvLanguagePriority(preferredLanguage, alternativeLanguage))
        {
            if (!available.Contains(language))
                continue;

            MediaTrack? track = typedTracks.FirstOrDefault(item => LanguageNormalizer.AreEquivalent(item.Language, language));
            if (track != null)
                return track.ID;
        }

        return null;
    }
}
