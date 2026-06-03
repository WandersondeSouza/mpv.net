using System.Text;
using System.Text.Json;
using System.Xml.Linq;

using MpvNet.Extensions;
using MpvNet.Help;

namespace MpvNet;

public sealed record PlaylistFileItem(string Path, string Title);

public static class PlaylistFile
{
    public static string WriteTempM3u(IEnumerable<PlaylistFileItem> items)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".m3u8");

        using StreamWriter writer = new(path, false, new UTF8Encoding(false));
        writer.WriteLine("#EXTM3U");

        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(item.Title))
                writer.WriteLine("#EXTINF:-1," + item.Title.Trim());

            writer.WriteLine(item.Path);
        }

        App.TempFiles.Add(path);
        return path;
    }

    public static List<PlaylistFileItem> Read(string path)
    {
        if (!File.Exists(path) || !FileTypes.IsPlaylistFile(path))
            return [];

        try
        {
            return path.Ext() switch
            {
                "m3u" or "m3u8" => ReadM3u(path),
                "pls" => ReadPls(path),
                "xspf" => ReadXspf(path),
                "asx" => ReadAsx(path),
                "wpl" => ReadWpl(path),
                "cue" => ReadCue(path),
                "jspf" => ReadJspf(path),
                _ => []
            };
        }
        catch (Exception ex)
        {
            Terminal.WriteError(ex);
            return [];
        }
    }

    public static List<PlaylistFileItem> Normalize(string playlistPath, IEnumerable<PlaylistFileItem> items)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<PlaylistFileItem> normalizedItems = [];

        foreach (var item in items)
        {
            string resolvedPath = ResolvePath(playlistPath, item.Path);

            if (!IsPlayableItem(resolvedPath))
                continue;

            string key = NormalizeKey(resolvedPath);

            if (!seen.Add(key))
                continue;

            normalizedItems.Add(new PlaylistFileItem(resolvedPath, GetDisplayTitle(resolvedPath, item.Title)));
        }

        return normalizedItems;
    }

    static List<PlaylistFileItem> ReadM3u(string path)
    {
        List<PlaylistFileItem> items = [];
        string title = "";

        foreach (string rawLine in File.ReadLines(path))
        {
            string line = rawLine.Trim();

            if (line.Length == 0)
                continue;

            if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                int comma = line.IndexOf(',');
                title = comma >= 0 ? line[(comma + 1)..].Trim() : "";
                continue;
            }

            if (line.StartsWith('#'))
                continue;

            items.Add(new PlaylistFileItem(line, title));
            title = "";
        }

        return Normalize(path, items);
    }

    static List<PlaylistFileItem> ReadPls(string path)
    {
        Dictionary<int, string> pathsByIndex = [];
        Dictionary<int, string> titles = [];

        foreach (string rawLine in File.ReadLines(path))
        {
            string line = rawLine.Trim();
            int equals = line.IndexOf('=');

            if (equals <= 0)
                continue;

            string name = line[..equals];
            string value = line[(equals + 1)..].Trim();

            if (TryReadNumberedKey(name, "File", out int fileIndex))
                pathsByIndex[fileIndex] = value;
            else if (TryReadNumberedKey(name, "Title", out int titleIndex))
                titles[titleIndex] = value;
        }

        return Normalize(path, pathsByIndex.OrderBy(pair => pair.Key).Select(pair =>
            new PlaylistFileItem(pair.Value, titles.GetValueOrDefault(pair.Key, ""))));
    }

    static List<PlaylistFileItem> ReadXspf(string path)
    {
        XDocument doc = XDocument.Load(path);

        return Normalize(path, ElementsByName(doc, "track", false).Select(track =>
            new PlaylistFileItem(
                ElementValue(track, "location", false),
                ElementValue(track, "title", false))));
    }

    static List<PlaylistFileItem> ReadAsx(string path)
    {
        XDocument doc = XDocument.Load(path);

        return Normalize(path, ElementsByName(doc, "entry").Select(entry =>
            new PlaylistFileItem(
                AttributeValue(DescendantByName(entry, "ref"), "href"),
                ElementValue(entry, "title"))));
    }

    static List<PlaylistFileItem> ReadWpl(string path)
    {
        XDocument doc = XDocument.Load(path);

        return Normalize(path, ElementsByName(doc, "media").Select(media =>
            new PlaylistFileItem(
                AttributeValue(media, "src"),
                AttributeValue(media, "title"))));
    }

    static List<PlaylistFileItem> ReadCue(string path)
    {
        List<PlaylistFileItem> items = [];
        string title = "";

        foreach (string rawLine in File.ReadLines(path))
        {
            string line = rawLine.Trim();

            if (line.StartsWith("TITLE ", StringComparison.OrdinalIgnoreCase))
                title = ReadCueValue(line[6..]);
            else if (line.StartsWith("FILE ", StringComparison.OrdinalIgnoreCase))
                items.Add(new PlaylistFileItem(ReadCueValue(line[5..]), title));
        }

        return Normalize(path, items);
    }

    static List<PlaylistFileItem> ReadJspf(string path)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = doc.RootElement;

        if (root.TryGetProperty("playlist", out JsonElement playlist))
            root = playlist;

        if (!root.TryGetProperty("track", out JsonElement tracks) || tracks.ValueKind != JsonValueKind.Array)
            return [];

        List<PlaylistFileItem> items = [];

        foreach (JsonElement track in tracks.EnumerateArray())
        {
            string title = track.TryGetProperty("title", out JsonElement titleElement) ? titleElement.GetString() ?? "" : "";
            string location = "";

            if (track.TryGetProperty("location", out JsonElement locationElement))
            {
                if (locationElement.ValueKind == JsonValueKind.Array)
                    location = locationElement.EnumerateArray().FirstOrDefault().GetString() ?? "";
                else if (locationElement.ValueKind == JsonValueKind.String)
                    location = locationElement.GetString() ?? "";
            }

            items.Add(new PlaylistFileItem(location, title));
        }

        return Normalize(path, items);
    }

    static bool TryReadNumberedKey(string name, string prefix, out int number)
    {
        number = 0;

        return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(name[prefix.Length..], out number);
    }

    static IEnumerable<XElement> ElementsByName(XContainer container, string localName, bool ignoreCase = true) =>
        container.Descendants().Where(element => HasLocalName(element, localName, ignoreCase));

    static XElement? DescendantByName(XContainer container, string localName) =>
        container.Descendants().FirstOrDefault(element => HasLocalName(element, localName));

    static string ElementValue(XContainer container, string localName, bool ignoreCase = true) =>
        container.Elements().FirstOrDefault(element => HasLocalName(element, localName, ignoreCase))?.Value.Trim() ?? "";

    static string AttributeValue(XElement? element, string localName) =>
        element?.Attributes().FirstOrDefault(attribute => HasLocalName(attribute, localName))?.Value.Trim() ?? "";

    static bool HasLocalName(XObject node, string localName, bool ignoreCase = true) =>
        node switch
        {
            XElement element => HasLocalName(element.Name.LocalName, localName, ignoreCase),
            XAttribute attribute => HasLocalName(attribute.Name.LocalName, localName, ignoreCase),
            _ => false
        };

    static bool HasLocalName(string value, string localName, bool ignoreCase) =>
        ignoreCase
            ? value.Equals(localName, StringComparison.OrdinalIgnoreCase)
            : value == localName;

    static string ReadCueValue(string value)
    {
        value = value.Trim();

        if (value.StartsWith('"'))
        {
            int end = value.IndexOf('"', 1);

            if (end > 1)
                return value[1..end];
        }

        int typeIndex = value.LastIndexOf(' ');
        return typeIndex > 0 ? value[..typeIndex].Trim() : value;
    }

    static string ResolvePath(string playlistPath, string itemPath)
    {
        itemPath = itemPath.Trim();

        if (itemPath.Length == 0 || FileTypes.IsStreamingUrl(itemPath))
            return itemPath;

        if (Uri.TryCreate(itemPath, UriKind.Absolute, out Uri? uri) && !uri.IsFile)
            return itemPath;

        itemPath = Uri.UnescapeDataString(itemPath.Replace("file:///", "").Replace("file://", ""));

        if (Path.IsPathFullyQualified(itemPath))
            return itemPath;

        string? dir = Path.GetDirectoryName(playlistPath);
        return string.IsNullOrEmpty(dir) ? itemPath : Path.GetFullPath(Path.Combine(dir, itemPath));
    }

    static bool IsPlayableItem(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (FileTypes.IsStreamingUrl(path))
            return true;

        string ext = path.Ext();
        return File.Exists(path) && (FileTypes.IsVideo(ext) || FileTypes.IsAudio(ext));
    }

    static string GetDisplayTitle(string path, string title)
    {
        string value = string.IsNullOrWhiteSpace(title) ? Path.GetFileName(path) : title;
        return TitleHelp.NormalizeMediaTitle(value);
    }

    static string NormalizeKey(string path)
    {
        if (FileTypes.IsStreamingUrl(path))
            return path.Trim();

        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
