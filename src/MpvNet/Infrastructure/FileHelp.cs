
namespace MpvNet.Help;

using System.Text;

public static class FileHelp
{
    public static void Delete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Terminal.WriteError("Failed to delete file:" + BR + path + BR + ex.Message);
        }
    }

    public static string ReadTextFile(string path) => File.Exists(path) ? File.ReadAllText(path) : "";

    public static void WriteAllTextAtomic(string path, string contents) =>
        WriteAllTextAtomic(path, contents, new UTF8Encoding(false));

    public static void WriteAllTextAtomic(string path, string contents, Encoding encoding)
    {
        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            File.WriteAllText(tempPath, contents, encoding);
            File.Move(tempPath, path, true);
        }
        catch
        {
            Delete(tempPath);
            throw;
        }
    }
}
