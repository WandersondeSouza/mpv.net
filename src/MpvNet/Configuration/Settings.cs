
using System.Text;
using System.Xml.Serialization;
using System.Xml;
using System.Drawing;

namespace MpvNet;

[Serializable()]
public class AppSettings
{
    public bool InputDefaultBindingsFixApplied;
    public bool ShowMenuFixApplied;
    public int MenuUpdateVersion;
    public int Volume = 100;
    public List<string> RecentFiles = new List<string>();
    public Point WindowLocation;
    public Point WindowPosition;
    public Size WindowSize;
    public string AudioDevice = "";
    public string ConfigEditorSearch = "Video:";
    public string Mute = "no";
    public string StartupFolder = "";
}

internal static class SettingsStore
{
    public static string SettingsFile => Player.ConfigFolder + "settings.xml";

    public static AppSettings Load()
    {
        Log.Debug("Loading application settings.");

        if (!File.Exists(SettingsFile))
        {
            Log.Debug("Application settings file was not found; using defaults.");
            return new AppSettings();
        }

        try
        {
            XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
            using FileStream fs = new FileStream(SettingsFile, FileMode.Open);
            var settings = (AppSettings)serializer.Deserialize(fs)!;
            Log.Debug("Application settings loaded.");
            return settings;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load application settings.");
            Terminal.WriteError(ex.ToString());
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        string tempFile = SettingsFile + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            using (XmlTextWriter writer = new XmlTextWriter(tempFile, Encoding.UTF8))
            {
                writer.Formatting = Formatting.Indented;
                writer.Indentation = 4;
                XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
                serializer.Serialize(writer, settings);
            }

            File.Move(tempFile, SettingsFile, true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save application settings.");
            Terminal.WriteError(ex.ToString());

            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch (Exception cleanupEx)
            {
                Log.Error(cleanupEx, "Failed to delete temporary settings file.");
                Terminal.WriteError(cleanupEx.ToString());
            }
        }
    }
}
