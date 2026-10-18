namespace MpvNet;

/// <summary>Edits directory entries without changing unrelated Windows PATH entries.</summary>
public static class WindowsPathEnvironment
{
    internal static string? NormalizeEntry(string? entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
            return null;
        string value = Environment.ExpandEnvironmentVariables(entry.Trim().Trim('"'));
        if (!Path.IsPathFullyQualified(value))
            return null;
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    public static bool ContainsEntry(string? path, string directory)
    {
        string? target = NormalizeEntry(directory);
        return target is not null && Entries(path).Any(entry =>
            string.Equals(NormalizeEntry(entry), target, StringComparison.OrdinalIgnoreCase));
    }

    public static string RemoveEntry(string? path, string directory)
    {
        string? target = NormalizeEntry(directory);
        return target is null ? path ?? "" : string.Join(Path.PathSeparator, Entries(path).Where(entry =>
            !string.Equals(NormalizeEntry(entry), target, StringComparison.OrdinalIgnoreCase)));
    }

    public static string PrependEntries(string? path, params string[] directories)
    {
        string[] prefix = directories.Select(NormalizeEntry).OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return string.Join(Path.PathSeparator, prefix.Concat(Entries(path).Where(entry =>
            !prefix.Contains(NormalizeEntry(entry), StringComparer.OrdinalIgnoreCase))));
    }

    internal static IEnumerable<string> Entries(string? path) =>
        string.IsNullOrEmpty(path) ? [] : path.Split(Path.PathSeparator);
}
