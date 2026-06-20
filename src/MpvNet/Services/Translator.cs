
namespace MpvNet;

/// <summary>
/// Stores the translation provider currently used by the application.
/// </summary>
public static class TranslationProvider
{
    public static ITranslator? Current { get; set; }
}

/// <summary>
/// Compatibility facade for the former translation provider name.
/// </summary>
[Obsolete($"Use {nameof(TranslationProvider)} instead.")]
public static class Translator
{
    public static ITranslator? Current
    {
        get => TranslationProvider.Current;
        set => TranslationProvider.Current = value;
    }
}

/// <summary>
/// Translates application text using the active localization backend.
/// </summary>
public interface ITranslator
{
    string Gettext(string messageId);
    string GetParticularString(string context, string text);
}
