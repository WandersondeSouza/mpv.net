namespace MpvNet;

internal static class ConfigFileParser
{
    public static Dictionary<string, string> ParseKeyValueLines(IEnumerable<string> lines)
    {
        Dictionary<string, string> values = [];

        foreach (string line in lines)
        {
            int separatorIndex = line.IndexOf('=');

            if (separatorIndex < 0 || line.StartsWith("#"))
                continue;

            string key = line[..separatorIndex].Trim();
            string value = line[(separatorIndex + 1)..].Trim();
            values[key] = value;
        }

        return values;
    }
}
