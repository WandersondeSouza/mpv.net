namespace MpvNet;

public static class NetworkCacheConfiguration
{
    // Reading is side-effect free. The editor persists this only on an actual save,
    // after backing up the old file. Preserve unrelated lines and comments.
    public static string[] Migrate(string[] lines)
    {
        Dictionary<string, string> values = ConfigFileParser.ParseKeyValueLines(
            lines.Select(line => line.Trim()).Where(line => !line.StartsWith('#')));
        if (!values.TryGetValue("network-cache-profile", out string? legacy))
            return lines;

        bool enabled = NetworkCachePolicy.NormalizeProfile(Unquote(legacy)) != "off" &&
            (!values.TryGetValue("automatic-network-cache", out string? automatic) || Unquote(automatic) == "yes");
        List<string> result = [];
        bool written = false;
        foreach (string line in lines)
        {
            string key = line.Trim().TrimStart('-').Split('=', 2)[0].Trim();
            if (key is "network-cache-profile" or "automatic-network-cache")
            {
                int comment = line.IndexOf('#');
                if (!written)
                {
                    result.Add("automatic-network-cache=" + (enabled ? "yes" : "no"));
                    written = true;
                }
                if (comment >= 0)
                    result.Add(line[comment..]);
            }
            else
                result.Add(line);
        }
        return result.ToArray();
    }

    static string Unquote(string value) => value.Split('#', 2)[0].Trim().Trim('"', '\'');
}
