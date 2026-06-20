
using MpvNet.Extensions;

namespace MpvNet.Windows;

public static class EditorConfigurationService
{
    public static List<Setting> ParseSettings(string content)
    {
        List<Setting> settingsList = new List<Setting>();

        foreach (EditorConfigurationSection? section in EditorConfigurationParser.Parse(content))
        {
            Setting? baseSetting = null;

            if (section.HasName("option"))
            {
                OptionSetting optionSetting = new OptionSetting();
                baseSetting = optionSetting;
                optionSetting.Default = section.GetValue("default");
                optionSetting.Value = optionSetting.Default;

                foreach (var it in section.GetValues("option"))
                {
                    var opt = new OptionSettingOption();

                    if (it.Value.ContainsEx(" "))
                    {
                        opt.Name = it.Value![..it.Value!.IndexOf(" ")];
                        opt.Help = it.Value[it.Value.IndexOf(" ")..].Trim();
                    }
                    else
                        opt.Name = it.Value;

                    opt.Text = EditorConfLocalization.TranslateOptionValue(opt.Name);

                    if (opt.Name == optionSetting.Default)
                        opt.Text += " " + _("(Default)");

                    opt.Help = EditorConfLocalization.TranslateText(opt.Help ?? "");

                    opt.OptionSetting = optionSetting;
                    optionSetting.Options.Add(opt);
                }
            }
            else
            {
                StringSetting stringSetting = new StringSetting();
                baseSetting = stringSetting;
                stringSetting.Default = section.HasName("default") ? section.GetValue("default") : "";
            }

            baseSetting.Name = EditorConfLocalization.TranslateText(section.GetValue("name") ?? "");            baseSetting.DisplayName = EditorConfLocalization.TranslateText(baseSetting.Name ?? "");            baseSetting.File = section.GetValue("file");
            baseSetting.Directory = EditorConfLocalization.TranslateDirectory(section.GetValue("directory") ?? "");

            if (section.HasName("help")) baseSetting.Help = EditorConfLocalization.TranslateText(section.GetValue("help") ?? "");
            if (section.HasName("url")) baseSetting.URL = section.GetValue("url");
            if (section.HasName("width")) baseSetting.Width = Convert.ToInt32(section.GetValue("width"));
            if (section.HasName("option-name-width")) baseSetting.OptionNameWidth = Convert.ToInt32(section.GetValue("option-name-width"));
            if (section.HasName("type")) baseSetting.Type = section.GetValue("type");

            if (baseSetting.Help.ContainsEx("\\n"))
                baseSetting.Help = baseSetting.Help?.Replace("\\n", "\n");

            settingsList.Add(baseSetting);
        }

        return settingsList;
    }
}

/// <summary>
/// Compatibility facade for the former configuration editor entry point.
/// </summary>
[Obsolete($"Use {nameof(EditorConfigurationService)} instead.")]
public static class Conf
{
    public static List<Setting> LoadConf(string content) =>
        EditorConfigurationService.ParseSettings(content);
}

static class EditorConfLocalization
{
    public static string TranslateDirectory(string path) =>
        string.Join("/", path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(TranslateText));

    public static string TranslateOptionValue(string? value) => TranslateText(value ?? "");

    public static string TranslateText(string text) => _(text);
}

public class ConfItem
{
    public string Comment { get; set; } = "";
    public string File { get; set; } = "";
    public string LineComment { get; set; } = "";
    public string Name { get; set; } = "";
    public string Section { get; set; } = "";
    public string Value { get; set; } = "";

    public bool IsSectionItem { get; set; }
    public Setting? SettingBase { get; set; }
}

internal static class EditorConfigurationParser
{
    public static List<EditorConfigurationSection> Parse(string content)
    {
        string[] lines = content.Split('\n');
        var sections = new List<EditorConfigurationSection>();
        EditorConfigurationSection? currentGroup = null;

        foreach (string it in lines)
        {
            string line = it.Trim();

            if (line.StartsWith('#'))
                continue;

            if (line == "")
            {
                currentGroup = new EditorConfigurationSection();
                sections.Add(currentGroup);
            }
            else if (line.Contains('='))
            {
                string name = line[..line.IndexOf('=')].Trim();
                string value = line[(line.IndexOf('=') + 1)..].Trim();

                currentGroup?.Items.Add(new StringPair(name, value));
            }
        }

        return sections;
    }
}

internal sealed class EditorConfigurationSection
{
    public List<StringPair> Items { get; set; } = new List<StringPair>();

    public bool HasName(string name)
    {
        foreach (var i in Items)
            if (i.Name == name)
                return true;

        return false;
    }

    public string? GetValue(string name)
    {
        foreach (var i in Items)
            if (i.Name == name)
                return i.Value;

        return null;
    }

    public List<StringPair> GetValues(string name) => Items.Where(i => i.Name == name).ToList();
}
