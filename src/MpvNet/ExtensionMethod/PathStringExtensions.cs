using System.IO;

namespace MpvNet.Extensions;

public static class PathStringExtensions
{
    public static string Ext(this string filepath) => GetExt(filepath, false);

    public static string FileName(this string filepath)
    {
        if (string.IsNullOrEmpty(filepath))
            return "";

        int index = filepath.LastIndexOf('\\');

        if (index > -1)
            return filepath[(index + 1)..];

        index = filepath.LastIndexOf('/');

        if (index > -1)
            return filepath[(index + 1)..];

        return filepath;
    }

    public static string ShortPath(this string filepath, int maxLength)
    {
        if (string.IsNullOrEmpty(filepath))
            return "";

        if (filepath.Length > maxLength && filepath.Substring(1, 2) == ":\\")
            filepath = $"{filepath[..3]}...\\{filepath.FileName()}";

        return filepath;
    }

    public static string Separator(this string filepath)
    {
        if (string.IsNullOrEmpty(filepath))
            return "";

        if (!filepath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            filepath = filepath + Path.DirectorySeparatorChar;

        return filepath;
    }

    private static string GetExt(string path, bool includeDot)
    {
        if (string.IsNullOrEmpty(path))
            return "";

        char[] chars = path.ToCharArray();

        for (int x = path.Length - 1; x >= 0; x--)
        {
            if (chars[x] == '/')
                return "";
            if (chars[x] == '\\')
                return "";
            if (chars[x] == '.')
                return path[(x + (includeDot ? 0 : 1))..].ToLowerInvariant();
        }

        return "";
    }
}
