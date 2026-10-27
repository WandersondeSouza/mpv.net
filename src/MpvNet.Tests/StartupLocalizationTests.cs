using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MpvNet.Tests;

public sealed class StartupLocalizationTests
{
    public static TheoryData<string, string> InterfaceLanguages
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (LanguageDefinition language in LanguageCatalog.InterfaceLanguages)
                data.Add(language.MpvNetName, language.CultureName);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(InterfaceLanguages))]
    public async Task CommandLineLanguageAppliesToTheInitialNativeMessage(string name, string culture)
    {
        var catalog = new NGettext.Catalog("mpvnet", AppPaths.Locale, new CultureInfo(culture));
        string expected = "${?playlist-playing-pos==-1:" + catalog.GetString("Drop files or URLs to play here.") + "}";
        string actual = await ReadStartupMessage("language=portuguese-brazil\n", "", "--language=" + name);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(LocalizationExpansionTests.NewLanguages), MemberType = typeof(LocalizationExpansionTests))]
    public async Task SavedLanguageIsUsedWhenNoCommandLineOverrideExists(string name, string culture, string native)
    {
        Assert.NotEmpty(native);
        var catalog = new NGettext.Catalog("mpvnet", AppPaths.Locale, new CultureInfo(culture));
        string expected = "${?playlist-playing-pos==-1:" + catalog.GetString("Drop files or URLs to play here.") + "}";
        Assert.Equal(expected, await ReadStartupMessage("language=" + name + "\n", ""));
    }

    [Theory]
    [InlineData("id-ID", "id-ID")]
    [InlineData("not-a-supported-language", "en")]
    public async Task LastLanguageArgumentRetainsAliasesAndFallback(string value, string culture)
    {
        var catalog = new NGettext.Catalog("mpvnet", AppPaths.Locale, new CultureInfo(culture));
        string expected = "${?playlist-playing-pos==-1:" + catalog.GetString("Drop files or URLs to play here.") + "}";
        Assert.Equal(expected, await ReadStartupMessage("language=portuguese-brazil\n", "",
            "--language=arabic", "--language=" + value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitNativeIdleMessageKeepsPrecedenceOverTheLocalizedDefault(bool commandLine)
    {
        string actual = await ReadStartupMessage("language=portuguese-brazil\n",
            commandLine ? "" : "osd-msg1=Custom idle message\n",
            "--language=indonesian", commandLine ? "--osd-msg1=Custom idle message" : "--pause=yes");
        Assert.Equal("Custom idle message", actual);
    }

    [Theory]
    [MemberData(nameof(LocalizationExpansionTests.NewLanguages), MemberType = typeof(LocalizationExpansionTests))]
    public async Task OscReceivesTheActiveCatalogMessages(string name, string culture, string native)
    {
        Assert.NotEmpty(native);
        var catalog = new NGettext.Catalog("mpvnet", AppPaths.Locale, new CultureInfo(culture));
        foreach (var (property, message) in new[] {
            ("user-data/mpvnet-osc-cache-label", "Cache"),
            ("user-data/mpvnet-osc-idle-message", "Drop files or URLs to play here.") })
            Assert.Equal(catalog.GetString(message), await ReadStartupProperty(property,
                "language=portuguese-brazil\n", "", "--language=" + name));
    }

    static Task<string> ReadStartupMessage(string frontendConfig, string mpvConfig, params string[] arguments) =>
        ReadStartupProperty("osd-msg1", frontendConfig, mpvConfig, arguments);

    static async Task<string> ReadStartupProperty(string property, string frontendConfig, string mpvConfig, params string[] arguments)
    {
        using TestDirectory directory = new();
        File.WriteAllText(Path.Combine(directory.Path, "mpvnet.conf"), frontendConfig);
        File.WriteAllText(Path.Combine(directory.Path, "mpv.conf"), mpvConfig);
        string pipeName = "mpvnet-startup-test-" + Guid.NewGuid().ToString("N");
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "mpvnet.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        foreach (string argument in new[] { "--config-dir=" + directory.Path,
            "--process-instance=multi", "--input-ipc-server=\\\\.\\pipe\\" + pipeName,
            "--vo=null", "--ao=null", "--idle=yes" })
            start.ArgumentList.Add(argument);
        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);
        start.Environment["MPVNET_LOCAL_APPDATA"] = Path.Combine(directory.Path, "data");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using Process process = Process.Start(start)!;
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeout.Token);
            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
            // The native IPC property verifies the text actually installed in mpv,
            // rather than just proving that the gettext entry exists.
            string query = JsonSerializer.Serialize(new { command = new[] { "get_property", property }, request_id = 1 });
            while (!timeout.IsCancellationRequested)
            {
                await writer.WriteLineAsync(query.AsMemory(), timeout.Token);
                while (await reader.ReadLineAsync(timeout.Token) is { } line)
                {
                    using JsonDocument response = JsonDocument.Parse(line);
                    if (!response.RootElement.TryGetProperty("request_id", out JsonElement request) || request.GetInt32() != 1)
                        continue;
                    // IPC may accept a connection just before the frontend
                    // publishes the translated script properties.
                    if (response.RootElement.GetProperty("error").GetString() != "success")
                        break;
                    return response.RootElement.GetProperty("data").GetString()!;
                }
                await Task.Delay(20, timeout.Token);
            }
            throw new IOException("The player closed its IPC connection before returning the initial message.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }
}
