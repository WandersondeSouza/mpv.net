namespace MpvNet;

public static class RecentMediaPolicy
{
    static readonly string[] SensitiveQueryKeyIndicators =
    [
        "auth",
        "bearer",
        "cookie",
        "credential",
        "jwt",
        "password",
        "passwd",
        "privatekey",
        "secret",
        "session",
        "signature",
        "token"
    ];

    public static bool ShouldPersist(string? value, bool saveNetworkUrls)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        MediaLoadRequest? request = MediaInputNormalizer.Normalize(value, MediaInputSource.RecentFiles);
        if (request is null || request.Input is "-" or "bd://" or "dvd://")
            return false;

        string input = request.Input;
        if (!input.Contains("://", StringComparison.Ordinal))
            return true;

        MediaInputClassification classification = MediaInputClassifier.Classify(input);
        if (!classification.IsValid || !Uri.TryCreate(input, UriKind.Absolute, out Uri? uri))
            return false;

        if (!string.IsNullOrEmpty(uri.UserInfo))
            return false;

        if (classification.Scheme == "file")
            return true;

        if (!classification.IsNetwork || !saveNetworkUrls || !string.IsNullOrEmpty(uri.Fragment))
            return false;

        return !ContainsSensitiveQueryKey(uri.Query);
    }

    public static List<string> FilterEntries(IEnumerable<string>? entries, bool saveNetworkUrls) =>
        entries?.Where(entry => ShouldPersist(entry, saveNetworkUrls)).ToList() ?? [];

    static bool ContainsSensitiveQueryKey(string query)
    {
        foreach (string parameter in query.TrimStart('?').Split(['&', ';'], StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = parameter.IndexOf('=');
            string rawKey = (separator < 0 ? parameter : parameter[..separator]).Replace('+', ' ');
            string decodedKey;

            try
            {
                decodedKey = Uri.UnescapeDataString(rawKey);
            }
            catch (UriFormatException)
            {
                return true;
            }

            string key = new(decodedKey
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());

            if (key.Length == 0 ||
                key == "key" ||
                key.EndsWith("apikey", StringComparison.Ordinal) ||
                key.EndsWith("authkey", StringComparison.Ordinal) ||
                key.Contains("keypair", StringComparison.Ordinal) ||
                key.Contains("keyid", StringComparison.Ordinal) ||
                key is "sig" or "hdnea" or "hdntl" or "hdnts" ||
                SensitiveQueryKeyIndicators.Any(indicator => key.Contains(indicator, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }
}
