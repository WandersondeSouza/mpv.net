using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using MpvNet.Help;
using MpvNet.Native;
using Xunit;

namespace MpvNet.Tests;

// These tests use the same normal libmpv prepared by the Windows build. They
// inspect the native queue, rather than just the strings prepared by C#.
public sealed class NativePlaylistTitleTests : IDisposable
{
    readonly string[] _existingTempFiles = Global.App.TempFiles.ToArray();

    public void Dispose()
    {
        foreach (string path in Global.App.TempFiles.Except(_existingTempFiles).ToArray())
        {
            File.Delete(path);
            Global.App.TempFiles.Remove(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecursiveCollectionPublishesAll200TitlesBeforePlayingTheNextItem(bool autoLoad)
    {
        using TestDirectory directory = new();
        string[] files = Enumerable.Range(1, 200).Select(index =>
            WriteWave(directory.Path, index == 1 ? "1.1 Introdução.mp4" : $"{index}.2 - GERANDO_o projeto.mp4")).ToArray();
        bool previous = Global.App.AutoLoadFolder;
        try
        {
            Global.App.AutoLoadFolder = autoLoad;
            using MainPlayer player = CreateNativePlayer();
            TaskCompletionSource loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
            player.FileLoaded += () => loaded.TrySetResult();
            player.LoadDiskFolder(directory.Path);
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await WaitUntil(() => player.GetPropertyInt("playlist-count") == 200);

            AssertTitles(player, files.Select(file => TitleHelp.NormalizeMediaTitle(Path.GetFileName(file))).ToArray());
            Assert.Equal(0, player.GetPropertyInt("playlist-pos"));
            string snapshot = player.GetPropertyString("playlist");
            await Task.Delay(700, TestContext.Current.CancellationToken);
            Assert.Equal(snapshot, player.GetPropertyString("playlist")); // No recurring rebuild/ID changes.
            Assert.Equal(TitleHelp.NormalizeMediaTitle(Path.GetFileName(files[0])), player.GetPropertyString("media-title"));

            player.CommandV("playlist-next", "force");
            await WaitUntil(() => player.GetPropertyString("path") == files[1]);
            AssertTitles(player, files.Select(file => TitleHelp.NormalizeMediaTitle(Path.GetFileName(file))).ToArray());
            player.CommandV("playlist-prev", "force");
            await WaitUntil(() => player.GetPropertyString("path") == files[0]);
        }
        finally { Global.App.AutoLoadFolder = previous; }
    }

    [Theory]
    [InlineData(MediaInputSource.FileDialog)]
    [InlineData(MediaInputSource.Clipboard)]
    [InlineData(MediaInputSource.DragAndDrop)]
    [InlineData(MediaInputSource.CommandLine)]
    [InlineData(MediaInputSource.InterProcessMessage)]
    public async Task DirectFilesAlreadyHaveNativeTitlesOnInsertionAndAppendPreservesThePlayingItem(MediaInputSource source)
    {
        using TestDirectory directory = new();
        string first = WriteWave(directory.Path, "0302 Class e Style 2 - Vue.js Completo - Origamid.mp4");
        string second = WriteWave(directory.Path, "1.2 - Gerando_o projeto.mp4");
        string third = WriteWave(directory.Path, "1.3 - Entendendo a estrutura.mp4");
        using MainPlayer player = CreateNativePlayer();
        player.LoadFiles([first, second], false, false, source: source);
        // Check immediately, without switching to or loading the second entry.
        AssertTitles(player, ["0302 Class E Style 2 Vue Js Completo Origamid", "1 2 Gerando O Projeto"]);
        await WaitUntil(() => player.GetPropertyBool("seekable", false));
        player.SetPropertyDouble("time-pos", 0.25);
        await WaitUntil(() => player.GetPropertyDouble("time-pos", false) >= 0.2);
        long id = player.GetPropertyLong("playlist/0/id");
        player.LoadFiles([third], false, true, source: source);
        AssertTitles(player, ["0302 Class E Style 2 Vue Js Completo Origamid", "1 2 Gerando O Projeto", "1 3 Entendendo A Estrutura"]);
        Assert.Equal(id, player.GetPropertyLong("playlist/0/id"));
        Assert.Equal(first, player.GetPropertyString("path"));
        Assert.True(player.GetPropertyDouble("time-pos") >= 0.2);
    }

    [Fact]
    public async Task LocalPlaylistPublishesExplicitTitlesBeforePlaybackAndKeepsThemOverEmbeddedTags()
    {
        using TestDirectory directory = new();
        string first = WriteWave(directory.Path, "original_name.mp4", "TAG_bruta - titulo.mp4");
        string second = WriteWave(directory.Path, "next_file.mp4");
        string playlist = Path.Combine(directory.Path, "list.m3u");
        File.WriteAllText(playlist, $"#EXTM3U\n#EXTINF:-1,TITULO_da playlist - especial\n{first}\n#EXTINF:-1,PROXIMO_video\n{second}\n");
        using MainPlayer player = CreateNativePlayer();
        TaskCompletionSource loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        player.FileLoaded += () => loaded.TrySetResult();
        player.LoadFiles([playlist], false, false, source: MediaInputSource.FileDialog);
        await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        AssertTitles(player, ["Titulo Da Playlist Especial", "Proximo Video"]);
        Assert.Equal("Titulo Da Playlist Especial", player.GetPropertyString("media-title"));
        Assert.Equal(first, player.GetPropertyString("path"));
    }

    [Theory]
    [InlineData(200)]
    [InlineData(2000)]
    public async Task PreparedCollectionsKeepTheNativePlaylistOriginSmallForSelectors(int count)
    {
        using TestDirectory directory = new();
        string first = WriteWave(directory.Path, "fixture.mp4");
        string playlist = Path.Combine(directory.Path, "large.m3u");
        string[] expected = Enumerable.Range(1, count).Select(index => $"{index} 2 Gerando O Projeto").ToArray();
        StringBuilder content = new("#EXTM3U\n");
        for (int index = 1; index <= count; index++)
        {
            content.AppendLine($"#EXTINF:-1,{index}.2 - GERANDO_o projeto.mp4");
            // Only the first, paused WAV is opened. The remaining URLs are
            // synthetic queue entries and never access the network.
            content.AppendLine(index == 1 ? first : $"https://example.invalid/video-{index}.mp4");
        }
        File.WriteAllText(playlist, content.ToString());
        using MainPlayer player = CreateNativePlayer();
        TaskCompletionSource loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        player.FileLoaded += () => loaded.TrySetResult();
        player.LoadFiles([playlist], false, false);
        await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(count, player.GetPropertyInt("playlist-count"));

        string origin = player.GetPropertyString("playlist/0/playlist-path");
        // A memory:// M3U makes this field contain the entire collection in
        // EVERY entry. Reading the full native playlist (as F8 does) then copies
        // an amount of text proportional to the square of the entry count.
        Assert.InRange(origin.Length, 1, 1024);
        Assert.True(File.Exists(origin));
        Assert.Contains(origin, Global.App.TempFiles);
        Assert.Equal(origin, player.GetPropertyString($"playlist/{count - 1}/playlist-path"));
        AssertTitles(player, expected);
        Assert.Equal(first, player.GetPropertyString("path"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task PreparedNetworkEntriesKeepYoutubePlaylistAndPerFileCacheOptions(bool explicitCache, bool explicitYtdl)
    {
        using TestDirectory directory = new();
        string media = WriteWave(directory.Path, "fixture.mp4");
        string playlist = Path.Combine(directory.Path, "list.m3u");
        // A hook redirects the stream to the local WAV before opening it. No
        // request to YouTube or other external service is made by this test.
        string url = "https://www.youtube.com/watch?v=fixture&list=synthetic&index=2";
        File.WriteAllText(playlist, $"#EXTM3U\n#EXTINF:-1,TITULO_da rede\n{url}\n");
        bool previousEnabled = Global.App.AutomaticNetworkCache;
        string previousProfile = Global.App.NetworkCacheProfile;
        try
        {
            Global.App.AutomaticNetworkCache = true;
            Global.App.NetworkCacheProfile = "resilient";
            using RedirectedNetworkPlayer player = new(media, explicitCache, explicitYtdl);
            CreateNativePlayer(player);
            if (explicitYtdl)
                Assert.True(player.RegisterHook("on_load", 4, 4));
            Assert.True(player.RegisterHook("on_load", 3, 6));
            TaskCompletionSource loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
            player.FileLoaded += () => loaded.TrySetResult();
            player.LoadFiles([playlist], false, false);
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Contains(explicitYtdl ? "no-playlist" : "yes-playlist", player.YtdlOptions);
            if (explicitYtdl)
                Assert.DoesNotContain("yes-playlist", player.YtdlOptions);
            Assert.Equal(explicitCache ? 9 : 5, player.GetPropertyDouble("options/cache-pause-wait"));
            Assert.Equal(url, player.GetPropertyString("path"));
            Assert.Equal("Titulo Da Rede", player.GetPropertyString("media-title"));
        }
        finally
        {
            Global.App.AutomaticNetworkCache = previousEnabled;
            Global.App.NetworkCacheProfile = previousProfile;
        }
    }

    sealed class RedirectedNetworkPlayer(string media, bool explicitCache, bool explicitYtdl) : MainPlayer
    {
        public string YtdlOptions { get; private set; } = "";
        internal override void OnHook(MpvEventSnapshot snapshot)
        {
            base.OnHook(snapshot);
            if (explicitYtdl && snapshot.ReplyUserData == 4)
                SetPropertyString("file-local-options/ytdl-raw-options", "no-playlist=");
            if (snapshot.ReplyUserData != 3)
                return;
            YtdlOptions = GetPropertyString("options/ytdl-raw-options");
            SetPropertyString("stream-open-filename", media);
            if (explicitCache)
                SetPropertyString("file-local-options/cache-pause-wait", "9");
        }
    }

    [Fact]
    public async Task NativeTitlesChangedAtTheSameAddressesArePreparedWithoutReloadingThePlayingItem()
    {
        using TestDirectory directory = new();
        string first = WriteWave(directory.Path, "first.mp4");
        string second = WriteWave(directory.Path, "second.mp4");
        using MainPlayer player = CreateNativePlayer();
        TaskCompletionSource firstLoaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        player.FileLoaded += () => firstLoaded.TrySetResult();
        player.LoadFiles([first, second], false, false);
        await firstLoaded.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        string list = Path.Combine(directory.Path, "updated.m3u");
        File.WriteAllText(list, $"#EXTM3U\n#EXTINF:-1,First\n{first}\n#EXTINF:-1,NEW_pending - title.mp4\n{second}\n");
        TaskCompletionSource secondLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
        player.FileLoaded += () => secondLoad.TrySetResult();
        player.CommandV("loadlist", list, "replace");
        await secondLoad.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        long playingId = player.GetPropertyLong("playlist/0/id");
        await WaitUntil(() => player.GetPropertyString("playlist/1/title") == "New Pending Title");
        AssertTitles(player, ["First", "New Pending Title"]);
        Assert.Equal(playingId, player.GetPropertyLong("playlist/0/id"));
        string snapshot = player.GetPropertyString("playlist");
        await Task.Delay(700, TestContext.Current.CancellationToken);
        Assert.Equal(snapshot, player.GetPropertyString("playlist"));
    }

    [Fact]
    public void YtDlpPathPreservesScriptOptionsAndLiteralPathCharacters()
    {
        using MainPlayer player = CreateNativePlayer();
        player.CommandV("change-list", "script-opts", "append", "osc-layout=box");
        player.CommandV("change-list", "script-opts", "append", "test-value=prefix,ytdl_hook-ytdl_path=not-a-setting");
        string path = "C:\\Vídeos, 日本語 = tools\\yt-dlp.exe";
        player.ConfigureYtDlpPath(ResolvedYtDlp(path));
        Assert.True(player.TryGetPropertyStringMap("options/script-opts", out var options));
        Assert.Equal("box", options["osc-layout"]);
        Assert.Equal("prefix,ytdl_hook-ytdl_path=not-a-setting", options["test-value"]);
        Assert.Equal(path, options[MainPlayer.YtDlpScriptOption]);
        Assert.False(player.TryGetPropertyStringMap("idle", out _));
        player.Dispose();
        Assert.False(player.TryGetPropertyStringMap("options/script-opts", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("C:\\custom\\yt-dlp.exe")]
    public void YtDlpPathKeepsAnExplicitScriptOptionIncludingAnEmptyValue(string path)
    {
        using MainPlayer player = CreateNativePlayer(options: [("script-opts", MainPlayer.YtDlpScriptOption + "=" + path)]);
        player.ConfigureYtDlpPath(ResolvedYtDlp("C:\\automatic\\yt-dlp.exe"));
        Assert.True(player.TryGetPropertyStringMap("options/script-opts", out var options));
        Assert.Equal(path, options[MainPlayer.YtDlpScriptOption]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void YtDlpPathRespectsMpvConfigAndProfiles(bool profile)
    {
        using TestDirectory directory = new();
        File.WriteAllText(Path.Combine(directory.Path, "mpv.conf"),
            (profile ? "profile=online\n[online]\n" : "") +
            "script-opts=osc-layout=box,ytdl_hook-ytdl_path=C:\\custom\\yt-dlp.exe\n");
        using MainPlayer player = CreateNativePlayer(options: [("config", "yes"), ("config-dir", directory.Path)]);
        player.ConfigureYtDlpPath(ResolvedYtDlp("C:\\automatic\\yt-dlp.exe"));
        Assert.True(player.TryGetPropertyStringMap("options/script-opts", out var options));
        Assert.Equal("C:\\custom\\yt-dlp.exe", options[MainPlayer.YtDlpScriptOption]);
        Assert.Equal("box", options["osc-layout"]);
    }

    [Theory]
    [InlineData("# ytdl_path=ignored\nall_formats=no\n", false)]
    [InlineData("  # comment\n  ytdl_path = C:\\custom\\yt-dlp.exe\n", true)]
    [InlineData("ytdl_path=\n", true)]
    public void YtDlpPathRespectsTheScriptConfigInTheEffectiveConfigDirectory(string content, bool explicitPath)
    {
        using TestDirectory directory = new();
        string folder = Path.Combine(directory.Path, "script-opts");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "ytdl_hook.conf"), content);
        using MainPlayer player = CreateNativePlayer(options: [("config-dir", directory.Path)]);
        player.ConfigureYtDlpPath(ResolvedYtDlp("C:\\automatic\\yt-dlp.exe"));
        Assert.True(player.TryGetPropertyStringMap("options/script-opts", out var options));
        Assert.Equal(!explicitPath, options.ContainsKey(MainPlayer.YtDlpScriptOption));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void YtDlpPathKeepsRuntimeChangesOrRemovalDuringBootstrap(bool remove)
    {
        using MainPlayer player = CreateNativePlayer();
        player.ConfigureYtDlpPath(ResolvedYtDlp("C:\\first\\yt-dlp.exe"));
        if (remove)
            player.CommandV("change-list", "script-opts", "clr", "");
        else
            player.CommandV("change-list", "script-opts", "append", MainPlayer.YtDlpScriptOption + "=custom.exe");
        player.ConfigureYtDlpPath(ResolvedYtDlp("C:\\new-generation\\yt-dlp.exe"));
        Assert.True(player.TryGetPropertyStringMap("options/script-opts", out var options));
        if (remove)
            Assert.False(options.ContainsKey(MainPlayer.YtDlpScriptOption));
        else
            Assert.Equal("custom.exe", options[MainPlayer.YtDlpScriptOption]);
    }

    [Fact]
    public async Task YtDlpBootstrapUpdatesOnlyTheAutomaticPathWithoutReloadingThePlaylist()
    {
        using TestDirectory directory = new();
        string media = WriteWave(directory.Path, "local.wav");
        using MainPlayer player = CreateNativePlayer();
        TaskCompletionSource loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        player.FileLoaded += () => loaded.TrySetResult();
        player.LoadFiles([media, media], false, false);
        await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        string playlist = player.GetPropertyString("playlist");
        player.ConfigureYtDlpPath(ResolvedYtDlp("C:\\invalid\\yt-dlp.exe") with { IsValid = false });
        Assert.True(player.TryGetPropertyStringMap("options/script-opts", out var before));
        Assert.False(before.ContainsKey(MainPlayer.YtDlpScriptOption));
        player.ConfigureYtDlpPath(ResolvedYtDlp("C:\\first\\yt-dlp.exe"));
        player.ConfigureYtDlpPath(ResolvedYtDlp("C:\\updated, geração\\yt-dlp.exe"));
        Assert.True(player.TryGetPropertyStringMap("options/script-opts", out var after));
        Assert.Equal("C:\\updated, geração\\yt-dlp.exe", after[MainPlayer.YtDlpScriptOption]);
        Assert.Equal(playlist, player.GetPropertyString("playlist"));
        Assert.Equal(0, player.GetPropertyInt("playlist-pos"));
    }

    static ComponentResolutionResult ResolvedYtDlp(string path) => new(
        "yt-dlp.exe", path, ComponentSource.ComponentCache, true, true, null, null, null);

    [Fact]
    public async Task BuiltInYtDlpHookUsesTheAutomaticPathAndItsBootstrapReplacement()
    {
        using TestDirectory directory = new();
        string first = Path.Combine(directory.Path, "extrator, versão.exe");
        string second = Path.Combine(directory.Path, "outro extrator.exe");
        // where.exe exits on yt-dlp arguments. It proves which subprocess the
        // real hook starts without downloading media or contacting a service.
        File.Copy(Path.Combine(Environment.SystemDirectory, "where.exe"), first);
        File.Copy(first, second);
        using MainPlayer player = CreateNativePlayer(options: [("ytdl", "yes")]);
        foreach (string path in new[] { first, second })
        {
            player.ConfigureYtDlpPath(ResolvedYtDlp(path));
            player.LoadFiles(["ytdl://https://example.invalid/probe"], false, false);
            await WaitUntil(() => player.TryGetPropertyString("user-data/mpv/ytdl/path", out string actual) &&
                StreamingFailureDiagnostics.DecodeDiagnosticString(actual) == path);
        }
    }

    static void AssertTitles(MainPlayer player, string[] expected)
    {
        using JsonDocument playlist = JsonDocument.Parse(player.GetPropertyString("playlist"));
        string[] actual = playlist.RootElement.EnumerateArray().Select(entry =>
            entry.TryGetProperty("title", out JsonElement title) ? title.GetString()! : "<missing title>").ToArray();
        Assert.Equal(expected, actual);
    }

    static MainPlayer CreateNativePlayer(MainPlayer? player = null, (string, string)[]? options = null)
    {
        EnsureNativeLibrary();
        player ??= new();
        try
        {
            player.MainHandle = LibMpv.mpv_create();
            Assert.NotEqual(IntPtr.Zero, player.MainHandle);
            foreach (var (key, value) in new[] { ("config", "no"), ("vo", "null"), ("ao", "null"),
                ("idle", "yes"), ("pause", "yes"), ("keep-open", "yes"), ("load-scripts", "no"), ("ytdl", "no") }.Concat(options ?? []))
            {
                Assert.True(LibMpv.mpv_set_option_string(player.MainHandle,
                    Encoding.UTF8.GetBytes(key + "\0"), Encoding.UTF8.GetBytes(value + "\0")) >= 0, key);
            }
            Assert.Equal(0, (int)LibMpv.mpv_initialize(player.MainHandle));
            player.SetMpvInitialized();
            player.Handle = LibMpv.mpv_create_client(player.MainHandle, "title-tests");
            Assert.True(player.RegisterHook("on_load", MainPlayer.PlaylistTitleHookUserData, 5));
            Assert.True(player.RegisterHook("on_load", MainPlayer.NetworkCacheHookUserData, 50));
            player.ObserveProperty("playlist", player.ScheduleAutocreatedPlaylistNormalization);
            player.TrackEventTask(Task.Run(() => player.EventLoop(player.PlayerCancellationToken), TestContext.Current.CancellationToken));
            player.TrackEventTask(Task.Run(() => player.MainEventLoop(player.PlayerCancellationToken), TestContext.Current.CancellationToken));
            return player;
        }
        catch { player.Dispose(); throw; }
    }

    static void EnsureNativeLibrary()
    {
        foreach (string name in new[] { "libmpv-2.dll", "MediaInfo.dll" })
        {
            string target = Path.Combine(AppContext.BaseDirectory, name);
            if (File.Exists(target))
                continue;
            DirectoryInfo? root = new(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "src/MpvNet.Windows/MpvNet.Windows.csproj")))
                root = root.Parent;
            Assert.NotNull(root);
            string configuration = typeof(NativePlaylistTitleTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
            string[] candidates = [
                Path.Combine(root.FullName, "src/MpvNet.Windows/bin", configuration, "win-x64", name),
                Path.Combine(root.FullName, "src/MpvNet.Windows/bin/x64", configuration, "win-x64", name)];
            string? source = candidates.FirstOrDefault(File.Exists);
            Assert.NotNull(source);
            File.Copy(source, target);
        }
        RuntimeComponents.RegisterNativeResolver();
    }

    static async Task WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20, TestContext.Current.CancellationToken);
        Assert.True(condition(), "Timed out waiting for native playback state.");
    }

    static string WriteWave(string root, string name, string? metadataTitle = null)
    {
        string path = Path.Combine(root, name);
        using BinaryWriter writer = new(File.Create(path));
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(0);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(16000);
        writer.Write((short)2); writer.Write((short)16);
        if (metadataTitle != null)
        {
            byte[] tag = Encoding.UTF8.GetBytes(metadataTitle + "\0");
            int padding = tag.Length % 2;
            writer.Write(Encoding.ASCII.GetBytes("LIST")); writer.Write(12 + tag.Length + padding);
            writer.Write(Encoding.ASCII.GetBytes("INFOINAM")); writer.Write(tag.Length); writer.Write(tag);
            if (padding != 0) writer.Write((byte)0);
        }
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(16000); writer.Write(new byte[16000]);
        long length = writer.BaseStream.Position;
        writer.Seek(4, SeekOrigin.Begin); writer.Write((int)length - 8);
        return path;
    }
}
