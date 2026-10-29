using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MpvNet.Windows;
using MpvNet.Windows.WPF;
using Xunit;

namespace MpvNet.Tests;

public sealed class LocalizationExpansionTests
{
    public static TheoryData<string, string> CultureCases => new()
    {
        { "zh-TW", "chinese-taiwan" }, { "zh_HK", "chinese-hongkong" },
        { "zh-Hant", "chinese-taiwan" }, { "zh-MO", "chinese-hongkong" },
        { "zh-Hant-HK", "chinese-hongkong" }, { "zh-Hant-MO", "chinese-hongkong" },
        { "zh-Hant-CN", "chinese-taiwan" }, { "zh-Hans-TW", "chinese-china" },
        { "zh", "chinese-china" }, { "zho", "chinese-china" },
        { "zh-Hans", "chinese-china" }, { "zh-SG", "chinese-china" },
        { "hi", "hindi" }, { "hi-IN", "hindi" }, { "hin", "hindi" },
        { "ar", "arabic" }, { "ar-SA", "arabic" }, { "ar-EG", "arabic" },
        { "ar-AE", "arabic" }, { "ara", "arabic" },
        { "uk", "ukrainian" }, { "uk-UA", "ukrainian" }, { "ukr", "ukrainian" },
        { "id", "indonesian" }, { "id-ID", "indonesian" }, { "ind", "indonesian" },
        { "ar-Latn", "english" }, { "zz-ZZ", "english" },
        { "pt-BR", "portuguese-brazil" }, { "zh-CN", "chinese-china" },
    };

    [Theory]
    [MemberData(nameof(CultureCases))]
    public void ManualStartupAndAlangResolveCulture(string input, string expected)
    {
        Assert.Equal(expected, LocalizationService.ResolveMpvNetLanguage(input));
        Assert.Equal(expected, LocalizationService.ResolveFromMpvLanguageList(input));
        string? normalized = LanguageNormalizer.Normalize(input);
        if (normalized != null)
            Assert.Equal(expected, LocalizationService.ResolveStartupLanguage(new CultureInfo(normalized)));
    }

    public static TheoryData<string, string, string> NewLanguages => new()
    {
        { "chinese-taiwan", "zh-TW", "繁體中文（台灣）" },
        { "chinese-hongkong", "zh-HK", "繁體中文（香港）" },
        { "hindi", "hi-IN", "हिन्दी" },
        { "arabic", "ar-SA", "العربية" },
        { "ukrainian", "uk-UA", "Українська" },
        { "indonesian", "id-ID", "Bahasa Indonesia" },
    };

    [Theory]
    [MemberData(nameof(NewLanguages))]
    public void NativeNamesPersistAndCatalogsLoad(string name, string culture, string native)
    {
        LanguageDefinition language = Assert.IsType<LanguageDefinition>(LanguageCatalog.FindInterfaceLanguage(name));
        Assert.Equal(native, language.NativeDisplayName);
        Assert.Equal(name, LocalizationService.ResolveMpvNetLanguage(native));
        Assert.Equal(culture, language.CultureName);

        using TestDirectory directory = new();
        string configPath = Path.Combine(directory.Path, "mpvnet.conf");
        File.WriteAllText(configPath, $"language={name}\n# preserved\nunknown=value\n");
        string saved = ConfigFileParser.ParseKeyValueLines(File.ReadAllLines(configPath))["language"];
        AppClass app = new();
        app.ProcessProperty("language", saved);
        Assert.Equal(name, app.Language);
        Assert.EndsWith("?language=" + culture, AppClass.GetDonationUrl(name));

        var catalog = new NGettext.Catalog("mpvnet", AppPaths.Locale, new CultureInfo(culture));
        Assert.NotEqual("Drop files or URLs to play here.", catalog.GetString("Drop files or URLs to play here."));
        Assert.NotEqual("No playable media was found in this drive or folder.", catalog.GetString("No playable media was found in this drive or folder."));
        Assert.NotEqual("The source service requires verification that you are not a bot.", catalog.GetString("The source service requires verification that you are not a bot."));
        Assert.NotEqual("Fullscreen", catalog.GetString("Fullscreen"));
        Assert.NotEqual("Center", catalog.GetParticularString("Move", "Center"));
        Assert.Contains("{0}", catalog.GetString("{0} does not exist. Would you like to create it?"));
        Assert.Equal("A future missing message", catalog.GetString("A future missing message"));
    }

    [Fact]
    public void ExpandedUiFallbackDoesNotRewriteMediaPriorities()
    {
        Assert.NotNull(typeof(LanguageFallbackResolver).GetMethod("GetFallbacks", [typeof(string), typeof(IEnumerable<string>), typeof(string)]));
        Assert.Equal(["ar-EG", "ar"], MediaLanguageService.BuildMpvLanguagePriority("ar-EG"));
        Assert.Equal(["hi-IN", "hi"], MediaLanguageService.BuildMpvLanguagePriority("hi-IN"));
        Assert.Equal(["uk-UA", "uk"], MediaLanguageService.BuildMpvLanguagePriority("uk-UA"));
        Assert.Equal(["id-ID", "id"], MediaLanguageService.BuildMpvLanguagePriority("id-ID"));
        Assert.Equal(["zh-TW", "zh-Hant"], MediaLanguageService.BuildMpvLanguagePriority("zh-TW"));
        Assert.Equal(["zh-CN"], MediaLanguageService.BuildMpvLanguagePriority("zh-SG"));
        Assert.Equal(["zh-TW", "en"], LanguageFallbackResolver.GetFallbacks("zh-Hant"));
        Assert.Equal("en", LocalizationCultureResolver.ResolveSupportedCulture("zh-Hant", ["zh-CN", "en"]));
    }

    [Theory]
    [MemberData(nameof(StartupLocalizationTests.InterfaceLanguages), MemberType = typeof(StartupLocalizationTests))]
    public void FileDialogLabelsUseTheCatalogAndKeepExtensionPatterns(string name, string culture) => OnWpfThread(() =>
    {
        string savedLanguage = Global.App.Language;
        ITranslator? savedTranslator = TranslationProvider.Current;
        CultureInfo savedUi = CultureInfo.CurrentUICulture;
        try
        {
            TranslationProvider.Current = null;
            string[] baseline = FileTypes.GetOpenFileDialogFilter().Split('|');
            Global.App.Language = name;
            TranslationProvider.Current = new WpfTranslator();
            var catalog = new NGettext.Catalog("mpvnet", AppPaths.Locale, new CultureInfo(culture));
            string[] actual = FileTypes.GetOpenFileDialogFilter().Split('|');
            Assert.Equal(10, actual.Length);
            string[] labels = ["Media and playlist files", "Video files", "Audio files", "Playlists", "All files"];
            for (int index = 0; index < labels.Length; index++)
            {
                Assert.Equal(catalog.GetString(labels[index]) + (index == 4 ? " (*.*)" : ""), actual[index * 2]);
                Assert.Equal(baseline[index * 2 + 1], actual[index * 2 + 1]);
            }
        }
        finally
        {
            Global.App.Language = savedLanguage;
            TranslationProvider.Current = savedTranslator;
            CultureInfo.CurrentUICulture = savedUi;
            InterfaceLayout.ApplyCulture(savedUi);
        }
    });

    [Fact]
    public void TranslatingEditorNeverTranslatesConfigurationKeys()
    {
        ITranslator? saved = TranslationProvider.Current;
        try
        {
            TranslationProvider.Current = new KeyTranslatingStub();
            var setting = Assert.Single(EditorConfigurationService.ParseSettings("\nname = language\nfile = mpvnet\noption = arabic"));
            Assert.Equal("language", setting.Name);
            Assert.Equal("Translated language", setting.DisplayName);
            Assert.Equal("Arabic", Assert.IsType<OptionSetting>(setting).Options[0].Text);
        }
        finally { TranslationProvider.Current = saved; }
    }

    [Fact]
    public void RtlAndRegionalFormattingSwitchBackWithoutTouchingTechnicalValues() => OnWpfThread(() =>
    {
        CultureInfo formatting = CultureInfo.CurrentCulture;
        CultureInfo ui = CultureInfo.CurrentUICulture;
        string language = Global.App.Language;
        ITranslator? translator = TranslationProvider.Current;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-EG");
            var realTranslator = new WpfTranslator();
            TranslationProvider.Current = realTranslator;
            Global.App.Language = "arabic";
            Assert.Equal("الصوت", realTranslator.Gettext("Audio"));
            Assert.Equal("ar-EG", CultureInfo.CurrentCulture.Name);
            Assert.Equal(string.Format(CultureInfo.CurrentCulture, "{0:N2}; {1:d}", 1234.5, new DateTime(2026, 10, 27)),
                NGettext.Wpf.Translation._("{0:N2}; {1:d}", 1234.5, new DateTime(2026, 10, 27)));
            Assert.Equal("ar-SA", CultureInfo.CurrentUICulture.Name);
            Assert.Equal(FlowDirection.RightToLeft, Application.Current.Resources["InterfaceFlowDirection"]);
            Assert.Equal("ar-EG", ((System.Windows.Markup.XmlLanguage)Application.Current.Resources["InterfaceXmlLanguage"]).IetfLanguageTag, ignoreCase: true);

            var control = new StringSettingControl(new StringSetting { Name = "title", Value = "C:\\Media\\video-01.mkv" });
            var host = new Window { Content = control };
            host.SetResourceReference(FrameworkElement.FlowDirectionProperty, "InterfaceFlowDirection");
            Assert.Equal(FlowDirection.RightToLeft, host.FlowDirection);
            var input = Assert.IsType<TextBox>(control.FindName("ValueTextBox"));
            Assert.Equal(FlowDirection.LeftToRight, input.FlowDirection);
            Assert.Equal("C:\\Media\\video-01.mkv", input.Text);
            Assert.Equal("\u2066Ctrl+Alt+F\u2069", InterfaceLayout.TechnicalText("Ctrl+Alt+F"));

            // This literal includes pipes but is not a gettext context expression.
            const string loopHelp = "<N|inf|no> Loop a single file N times. inf means forever, no means normal playback.\\n\\nThe difference to loop-playlist is that this doesn't loop the playlist, just the file itself. If the playlist contains only a single file, the difference between the two option is that this option performs a seek on loop, instead of reloading the file. loop is an alias for this option.";
            Assert.StartsWith("<N|inf|no>", realTranslator.Gettext(loopHelp));
            Assert.NotEqual(loopHelp, realTranslator.Gettext(loopHelp));

            Global.App.Language = "english";
            Assert.Equal("Audio", realTranslator.Gettext("Audio"));
            Assert.Equal(FlowDirection.LeftToRight, host.FlowDirection);
            Assert.Equal("Ctrl+Alt+F", InterfaceLayout.TechnicalText("Ctrl+Alt+F"));
            Assert.Equal("ar-EG", CultureInfo.CurrentCulture.Name);
            host.Close();
        }
        finally
        {
            Global.App.Language = language;
            TranslationProvider.Current = translator;
            CultureInfo.CurrentCulture = formatting;
            CultureInfo.CurrentUICulture = ui;
            InterfaceLayout.ApplyCulture(ui);
        }
    });

    static readonly string[] LanguageDisplayNames =
    [
        "Bulgarian", "Chinese (Simplified)", "Chinese (Traditional, Taiwan)",
        "Chinese (Traditional, Hong Kong)", "Hindi", "Arabic", "Ukrainian",
        "Indonesian", "English", "Spanish", "French", "German", "Italian",
        "Japanese", "Korean", "Polish", "Portuguese (Brazil)",
        "Portuguese (Portugal)", "Russian", "Turkish",
    ];

    [Theory]
    [MemberData(nameof(StartupLocalizationTests.InterfaceLanguages), MemberType = typeof(StartupLocalizationTests))]
    public void LanguageOptionsUseInterfaceTranslationsAndKeepConfigurationValues(string name, string culture) => OnWpfThread(() =>
    {
        string savedLanguage = Global.App.Language;
        ITranslator? savedTranslator = TranslationProvider.Current;
        CultureInfo savedUi = CultureInfo.CurrentUICulture;
        try
        {
            Global.App.Language = name;
            TranslationProvider.Current = new WpfTranslator();
            var catalog = new NGettext.Catalog("mpvnet", AppPaths.Locale, new CultureInfo(culture));
            var resources = new System.Resources.ResourceManager("MpvNet.Windows.Properties.Resources", typeof(ConfWindow).Assembly);
            var options = Assert.IsType<OptionSetting>(EditorConfigurationService.ParseSettings(
                resources.GetString("editor_conf")!.TrimEnd()).Single(s => s.Name == "language")).Options;
            Assert.Equal(20, options.Count);
            for (int index = 0; index < options.Count; index++)
            {
                Assert.Equal(LanguageCatalog.InterfaceLanguages[index].MpvNetName, options[index].Name);
                string expected = catalog.GetString(LanguageDisplayNames[index]);
                if (index == 8)
                    expected += " " + catalog.GetString("(Default)");
                Assert.Equal(expected, options[index].Text);
                if (name != "english" && LanguageDisplayNames[index] != "Hindi")
                    Assert.NotEqual(LanguageDisplayNames[index], catalog.GetString(LanguageDisplayNames[index]));
            }
            Assert.Equal(name switch
            {
                "portuguese-brazil" or "portuguese-portugal" => "Francês",
                "english" => "French",
                "french" => "Français",
                _ => catalog.GetString("French"),
            }, options.Single(o => o.Name == "french").Text);
        }
        finally
        {
            Global.App.Language = savedLanguage;
            TranslationProvider.Current = savedTranslator;
            CultureInfo.CurrentUICulture = savedUi;
            InterfaceLayout.ApplyCulture(savedUi);
        }
    });

    [Fact]
    public void EditorChangesLanguageAndReturnsWithoutDiscardingUnsavedValues() => OnWpfThread(() =>
    {
        string language = Global.App.Language;
        ITranslator? translator = TranslationProvider.Current;
        string search = Global.App.Settings.ConfigEditorSearch;
        FieldInfo configFolder = typeof(MainPlayer).GetField("_configFolder", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object? originalFolder = configFolder.GetValue(Global.Player);
        CultureInfo ui = CultureInfo.CurrentUICulture;
        using TestDirectory directory = new();
        try
        {
            configFolder.SetValue(Global.Player, directory.Path + Path.DirectorySeparatorChar);
            TranslationProvider.Current = new WpfTranslator();
            Global.App.Language = "english";
            TranslationProvider.Current.Gettext("");
            Global.App.Settings.ConfigEditorSearch = "Category from previous locale:";
            File.WriteAllText(Global.App.ConfPath, "language=zh-Hant\nunknown=value\n");
            var window = new ConfWindow();
            Assert.Equal("General:", window.SearchText);
            var settings = (List<Setting>)typeof(ConfWindow).GetField("_settings", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            Setting selected = settings.Single(s => s.Name == "language");
            Assert.Equal("chinese-taiwan", selected.Value);
            Setting width = settings.First(s => s.Name == "recent-count");
            width.Value = "42";
            var languageControl = ((StackPanel)window.FindName("MainStackPanel")).Children.OfType<ComboBoxSettingControl>().Single(c => c.Setting.Name == "language");
            var combo = (ComboBox)languageControl.FindName("ComboBoxControl");
            var options = Assert.IsType<OptionSetting>(selected).Options;
            var label = new TextBlock();
            label.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(OptionSettingOption.Text))
            { Source = options.Single(o => o.Name == "chinese-taiwan") });
            foreach (string name in LanguageCatalog.InterfaceLanguages.Select(l => l.MpvNetName)
                .Concat(["portuguese-brazil", "english", "french", "chinese-taiwan"]))
            {
                combo.SelectedItem = options.Single(o => o.Name == name);
                Assert.Equal(name, Global.App.Language);
                Assert.Equal("42", width.Value);
                Assert.Equal(name == "arabic" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, window.FlowDirection);
                Assert.Equal(20, combo.Items.Count);
                Assert.Equal(name, selected.Value);
                var catalog = new NGettext.Catalog("mpvnet", AppPaths.Locale, LocalizationService.GetCulture(name));
                Assert.Equal(catalog.GetString("Chinese (Traditional, Taiwan)"), label.Text);
                for (int index = 0; index < options.Count; index++)
                    Assert.Equal(catalog.GetString(LanguageDisplayNames[index]) +
                        (options[index].Name == "english" ? " " + catalog.GetString("(Default)") : ""), options[index].Text);
            }
            // Saving another option must preserve the existing language alias.
            var content = (string)typeof(ConfWindow).GetMethod("GetContent", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, ["mpvnet"])!;
            Assert.Equal("zh-Hant", ConfigFileParser.ParseKeyValueLines(content.Split('\n'))["language"]);
            Assert.Equal("42", ConfigFileParser.ParseKeyValueLines(content.Split('\n'))["recent-count"]);
            Assert.Contains("unknown", content);
            // Restore edits so closing does not apply settings to the native player.
            width.Value = width.StartValue;
            window.Close();
        }
        finally
        {
            configFolder.SetValue(Global.Player, originalFolder);
            Global.App.Language = language;
            Global.App.Settings.ConfigEditorSearch = search;
            TranslationProvider.Current = translator;
            CultureInfo.CurrentUICulture = ui;
            InterfaceLayout.ApplyCulture(ui);
        }
    });

    static readonly Lazy<Dispatcher> WpfDispatcher = new(() =>
    {
        Dispatcher? dispatcher = null;
        using ManualResetEventSlim ready = new();
        var thread = new Thread(() =>
        {
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            dispatcher = application.Dispatcher;
            WpfApplication.Init();
            ready.Set();
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(15)), "WPF test initialization timed out.");
        return dispatcher!;
    });

    static void OnWpfThread(Action action) => WpfDispatcher.Value.Invoke(action);

    [Theory]
    [MemberData(nameof(NewLanguages))]
    public void WindowsCompositeFontRendersEveryNewScript(string name, string culture, string native) => OnWpfThread(() =>
    {
        Assert.Equal(native, LanguageCatalog.FindInterfaceLanguage(name)!.NativeDisplayName);
        var text = new FormattedText(native, new CultureInfo(culture),
            culture == "ar-SA" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            new Typeface("Global User Interface"), 18, Brushes.Black, 1);
        var drawing = new DrawingGroup();
        using (DrawingContext context = drawing.Open())
            context.DrawText(text, new Point(0, 0));
        GlyphRun[] runs = GlyphRuns(drawing).ToArray();
        Assert.NotEmpty(runs);
        Assert.All(runs, run => Assert.DoesNotContain((ushort)0, run.GlyphIndices));
    });

    static IEnumerable<GlyphRun> GlyphRuns(Drawing drawing)
    {
        if (drawing is GlyphRunDrawing glyph)
            yield return glyph.GlyphRun;
        if (drawing is DrawingGroup group)
            foreach (Drawing child in group.Children)
                foreach (GlyphRun run in GlyphRuns(child))
                    yield return run;
    }

    sealed class KeyTranslatingStub : ITranslator
    {
        public string Gettext(string msgId) => msgId == "language" ? "Translated language" : msgId;
        public string GetParticularString(string context, string text) => text;
    }
}
