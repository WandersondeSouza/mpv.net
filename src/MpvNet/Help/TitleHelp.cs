using System.IO;
using System.Text.RegularExpressions;

namespace MpvNet.Help;

public static class TitleHelp
{
    static readonly Regex SpacesRegex = new(@"\s+", RegexOptions.Compiled);
    static readonly Regex MpvNetSuffixRegex = new(@"\s+[-|]\s+mpv(?:\.net)?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string RetirarCaracteresExtendidos(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        const string invalidChars = "'\u00b4`^\u00a8~#$*()+=-_[{}]|/;:><\\\u00b0\u00ba\u00aa" +
            "\u00ac\u00a2\u00a3\u00b3\u00b2\u00b9\u00a7\u00bd\u00bc\u00be\u00b1\u00a9" +
            "\u00b5\u2021\u0161\u2030\u2729";

        foreach (char c in invalidChars)
            texto = texto.Replace(c, ' ');

        return NormalizeSpaces(texto);
    }

    public static string PrimeiraLetraDaPalavraMaiuscula(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        string normalizedText = NormalizeSpaces(texto);
        string[] words = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return string.Join(" ", words.Select(FormatWord));
    }

    public static string NormalizeMediaTitle(string texto) =>
        PrimeiraLetraDaPalavraMaiuscula(
            RetirarCaracteresExtendidos(
                RemoveDots(
                    RemoveSupportedExtension(
                        RemoveMpvNetSuffix(texto)))));

    public static string RemoveMpvNetSuffix(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        return MpvNetSuffixRegex.Replace(texto, string.Empty);
    }

    static string RemoveSupportedExtension(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        string value = texto.Trim();
        string ext = Path.GetExtension(value);

        if (string.IsNullOrEmpty(ext))
            return value;

        string normalizedExt = ext.TrimStart('.').ToLowerInvariant();

        if (!FileTypes.IsVideo(normalizedExt) &&
            !FileTypes.IsAudio(normalizedExt) &&
            !FileTypes.IsPlaylist(normalizedExt) &&
            !FileTypes.Subtitle.Contains(normalizedExt))
        {
            return value;
        }

        return value[..^ext.Length];
    }

    static string RemoveDots(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        return texto.Replace('.', ' ');
    }

    static string NormalizeSpaces(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        return SpacesRegex.Replace(texto.Trim(), " ");
    }

    static string FormatWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return string.Empty;

        string normalizedWord = word.Trim();
        return string.Concat(char.ToUpper(normalizedWord[0]), normalizedWord[1..].ToLower());
    }
}
