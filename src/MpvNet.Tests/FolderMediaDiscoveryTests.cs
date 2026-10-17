using System;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace MpvNet.Tests;

public sealed class FolderMediaDiscoveryTests
{
    static string Write(string root, string relative, string content = "")
    {
        string path = Path.GetFullPath(Path.Combine(root, relative));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void FindsAudioAndVideoAtEveryLevelInNaturalRelativePathOrder()
    {
        using TestDirectory directory = new();
        string ten = Write(directory.Path, "Season 10/episode 1.mkv");
        string two = Write(directory.Path, "Season 2/deep/episode 10.mp4");
        string first = Write(directory.Path, "Season 2/deep/episode 2.mp3");
        foreach (string name in new[] { "image.jpg", "subtitle.srt", "script.lua", "unknown.bin", "disc.iso" })
            Write(directory.Path, name);

        var selection = FolderMediaDiscovery.Discover(directory.Path, CancellationToken.None, new ReverseFileSystem());

        Assert.Equal(FolderMediaKind.Collection, selection.Kind);
        Assert.True(selection.RootAccessible);
        Assert.Equal(new[] { first, two, ten }, selection.Items.Select(item => item.Path));
        Assert.Equal("Episode 2", selection.Items[0].Title);
    }

    [Fact]
    public void ExpandsSubfolderPlaylistsDeduplicatesAndPrefersPlaylistTitle()
    {
        using TestDirectory directory = new();
        string first = Write(directory.Path, "A/movie_name.mp4");
        string second = Write(directory.Path, "B/movie_name.mp4");
        Write(directory.Path, "Z/nested/list.m3u", "#EXTM3U\n#EXTINF:-1,Custom title\n../../A/movie_name.mp4\n../../A/MOVIE_NAME.MP4\n../../B/movie_name.mp4\nhttps://example.invalid/live\n");

        var selection = FolderMediaDiscovery.Discover(directory.Path, CancellationToken.None);

        Assert.Equal(3, selection.Items.Count);
        Assert.Equal(first, selection.Items[0].Path);
        Assert.Equal("Custom Title", selection.Items[0].Title);
        Assert.Equal(second, selection.Items[1].Path);
        Assert.Equal("https://example.invalid/live", selection.Items[2].Path);
        Assert.DoesNotContain(directory.Path, selection.Items[0].Title);
    }

    [Theory]
    [InlineData("VIDEO_TS", "VIDEO_TS.IFO", 1)]
    [InlineData("BDMV", "index.bdmv", 2)]
    public void RecognizesOpticalRootAndInnerFolder(string folder, string marker, int kind)
    {
        using TestDirectory directory = new();
        Write(directory.Path, $"{folder}/{marker}");
        foreach (string path in new[] { directory.Path, Path.Combine(directory.Path, folder) })
        {
            var selection = FolderMediaDiscovery.Discover(path, CancellationToken.None);
            Assert.Equal((FolderMediaKind)kind, selection.Kind);
            Assert.Equal(directory.Path, selection.Root);
            Assert.Empty(selection.Items);
        }
    }

    [Fact]
    public void CollectionsExcludeNestedDiscsAndOpticalSegmentsReferencedByPlaylists()
    {
        using TestDirectory directory = new();
        string movie = Write(directory.Path, "movie.mp4");
        Write(directory.Path, "DVD/VIDEO_TS/VIDEO_TS.IFO");
        Write(directory.Path, "DVD/VIDEO_TS/VTS_01_1.VOB");
        Write(directory.Path, "BD/BDMV/index.bdmv");
        Write(directory.Path, "BD/BDMV/STREAM/00001.m2ts");
        Write(directory.Path, "list.m3u", "DVD/VIDEO_TS/VTS_01_1.VOB\nBD/BDMV/STREAM/00001.m2ts\n");

        var selection = FolderMediaDiscovery.Discover(directory.Path, CancellationToken.None);
        Assert.Equal(movie, Assert.Single(selection.Items).Path);
        Assert.Equal(2, selection.SkippedEntries);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void PartialAccessOrRemovalOrIoFailureDoesNotLoseOtherItems(int failure)
    {
        using TestDirectory directory = new();
        string movie = Write(directory.Path, "good/movie.mp4");
        string inaccessible = Path.GetDirectoryName(Write(directory.Path, "bad/hidden.mp3"))!;

        var selection = FolderMediaDiscovery.Discover(directory.Path, CancellationToken.None,
            new FailingFileSystem(inaccessible, failure));

        Assert.True(selection.RootAccessible);
        Assert.Equal(movie, Assert.Single(selection.Items).Path);
        Assert.Equal(1, selection.Failures);
    }

    [Fact]
    public void ReparseDirectoriesAndFilesAreSkippedIncludingPlaylistReferences()
    {
        using TestDirectory directory = new();
        string linkedDirectory = Path.GetDirectoryName(Write(directory.Path, "link/movie.mp4"))!;
        string linkedFile = Write(directory.Path, "linked.mp3");
        string good = Write(directory.Path, "good.mp4");
        Write(directory.Path, "list.m3u", "link/movie.mp4\nlinked.mp3\n");
        var selection = FolderMediaDiscovery.Discover(directory.Path, CancellationToken.None,
            new ReparseFileSystem(linkedDirectory, linkedFile));
        Assert.Equal(good, Assert.Single(selection.Items).Path);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExplicitCollectionIgnoresAutoLoadAndDisarmsPendingAutoload(bool autoLoad)
    {
        using TestDirectory directory = new();
        Write(directory.Path, "deep/movie.mp4");
        bool original = Global.App.AutoLoadFolder;
        try
        {
            Global.App.AutoLoadFolder = autoLoad;
            using MainPlayer player = new();
            player.LastLoad = DateTime.Now; // Opening within the normal append grace period.
            player.ArmAutoLoadFolder(true);
            var selection = FolderMediaDiscovery.Discover(directory.Path, CancellationToken.None);
            Assert.True(player.ApplyFolderMediaSelection(selection, CancellationToken.None));
            Assert.False(player.TryConsumeAutoLoadFolderRequest());
            Assert.Equal(autoLoad, Global.App.AutoLoadFolder);
        }
        finally { Global.App.AutoLoadFolder = original; }
    }

    [Fact]
    public void EmptyAndInvalidAndUnreadableRootsLeaveCurrentLoadStateIntact()
    {
        using TestDirectory directory = new();
        using MainPlayer player = new();
        DateTime lastLoad = DateTime.Now;
        player.LastLoad = lastLoad;
        player.ArmAutoLoadFolder(true);
        var empty = FolderMediaDiscovery.Discover(directory.Path, CancellationToken.None);
        var missing = FolderMediaDiscovery.Discover(Path.Combine(directory.Path, "missing"), CancellationToken.None);
        var unreadable = FolderMediaDiscovery.Discover(directory.Path, CancellationToken.None, new FailingFileSystem(directory.Path, 0));
        Assert.True(empty.RootAccessible);
        Assert.False(missing.RootAccessible);
        Assert.False(unreadable.RootAccessible);
        foreach (var selection in new[] { empty, missing, unreadable })
            Assert.False(player.ApplyFolderMediaSelection(selection, CancellationToken.None));
        Assert.Equal(lastLoad, player.LastLoad);
        Assert.True(player.TryConsumeAutoLoadFolderRequest());
    }

    [Fact]
    public void CancellationDoesNotCommitPartialDiscovery()
    {
        using TestDirectory directory = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => FolderMediaDiscovery.Discover(directory.Path, cancellation.Token));
    }

    [Fact]
    public void IncompleteOpticalFoldersAndBrokenPlaylistsDoNotAddSegmentsOrLoseOtherMedia()
    {
        using TestDirectory directory = new();
        string movie = Write(directory.Path, "movie.mp4");
        Write(directory.Path, "VIDEO_TS/VTS_01_1.VOB");
        Write(directory.Path, "BDMV/STREAM/00001.m2ts");
        Write(directory.Path, "broken.xspf", "<invalid>");
        var selection = FolderMediaDiscovery.Discover(directory.Path, CancellationToken.None);
        Assert.Equal(FolderMediaKind.Collection, selection.Kind);
        Assert.Equal(movie, Assert.Single(selection.Items).Path);
        Assert.Equal(1, selection.Failures);
    }

    [Fact]
    public void ASelectedReparseRootIsNotTraversed()
    {
        using TestDirectory directory = new();
        Write(directory.Path, "movie.mp4");
        var selection = FolderMediaDiscovery.Discover(directory.Path, CancellationToken.None,
            new ReparseFileSystem(directory.Path));
        Assert.False(selection.RootAccessible);
        Assert.Empty(selection.Items);
    }

    sealed class ReverseFileSystem : FolderMediaFileSystem
    {
        public override string[] GetEntries(string path) => base.GetEntries(path).Reverse().ToArray();
    }

    sealed class FailingFileSystem(string failingPath, int failure) : FolderMediaFileSystem
    {
        public override string[] GetEntries(string path) => path == failingPath
            ? throw failure switch { 0 => new UnauthorizedAccessException(), 1 => new DirectoryNotFoundException(), _ => new IOException("Drive not ready") }
            : base.GetEntries(path);
    }

    sealed class ReparseFileSystem(params string[] linkedPaths) : FolderMediaFileSystem
    {
        public override FileAttributes GetAttributes(string path) =>
            base.GetAttributes(path) | (linkedPaths.Contains(path) ? FileAttributes.ReparsePoint : 0);
    }
}
