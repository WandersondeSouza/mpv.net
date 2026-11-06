using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using MpvNet.Windows.Services.MediaTransport;
using Xunit;

namespace MpvNet.Tests;

public sealed class ArtistArtworkTests
{
    const string ArtistId = "8bfac288-ccc5-448d-9573-c33ea2aa5c30";
    const string ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/a/a2/Portrait.png";
    static CancellationToken Token => TestContext.Current.CancellationToken;
    static readonly AudioArtistIdentity Artist = new("Example Artist", ArtistId);
    static Task<AlbumArtworkPixels?> NoEmbedded(CancellationToken _) => Task.FromResult<AlbumArtworkPixels?>(null);

    [Theory]
    [InlineData("PD", true, 4)]
    [InlineData("CC0", false, 5)]
    public async Task VerifiedArtistIsAvailableOfflineAndAlbumArtAlwaysWins(string license, bool useId, int calls)
    {
        using TestDirectory directory = new();
        var cache = new AudioArtworkCache(Path.Combine(directory.Path, "Artwork"));
        byte[] png = await AudioArtworkImage.EncodeAsync(new(1, 1, [255, 0, 0, 255]), Token);
        using var provider = new Provider(png, license);
        using var online = Connect(cache, provider);
        AudioArtistIdentity artist = useId ? Artist : Artist with { ArtistId = null };
        string media = Path.Combine(directory.Path, "audio.wav");
        await File.WriteAllBytesAsync(media, [1], Token);
        var album = new AudioArtworkIdentity("Example Artist", "Example Album");
        var request = new AudioArtworkRequest(media, true, false, false, null, "auto", "embedded-first", true,
            false, true, album, artist);
        var resolver = new AudioArtworkResolver(cache, online);
        Assert.Equal("default", (await resolver.ResolveAsync(request, NoEmbedded, Token))?.Source);
        Assert.Equal(0, provider.Requests);

        string? photo = await online.FindArtistAsync(artist, true, Token);
        Assert.NotNull(photo);
        var cached = await resolver.ResolveAsync(request, NoEmbedded, Token);
        Assert.Equal("artist", cached?.Source);
        Assert.Equal(photo, cached?.Path);
        Assert.Equal(calls, provider.Requests);

        string cover = Path.Combine(directory.Path, "cover.png");
        await File.WriteAllBytesAsync(cover, png, Token);
        Assert.Equal("local", (await resolver.ResolveAsync(request with { SelectedCoverPath = cover },
            NoEmbedded, Token))?.Source);
        Assert.Equal("local", (await resolver.ResolveAsync(request with { HasEmbeddedCover = true },
            _ => Task.FromResult<AlbumArtworkPixels?>(new(1, 1, [0, 255, 0, 255])), Token))?.Source);
        string albumPath = await cache.StoreImageAsync(
            await AudioArtworkImage.EncodeAsync(new(1, 1, [0, 0, 255, 255]), Token), Token);
        await cache.RememberAsync(album.Key!, albumPath, TimeSpan.FromDays(30), Token);
        Assert.Equal("online", (await resolver.ResolveAsync(request, NoEmbedded, Token))?.Source);
        Assert.Equal(calls, provider.Requests);

        // A different, still decodable PNG under the cached filename cannot
        // inherit the Commons provenance.
        File.Copy(albumPath, photo, overwrite: true);
        Assert.Null(await online.FindArtistAsync(artist, false, Token));
        Assert.Equal(calls, provider.Requests);
    }

    [Theory]
    [InlineData("ambiguous", 1)]
    [InlineData("truncated", 1)]
    [InlineData("various", 0)]
    [InlineData("multiple", 0)]
    [InlineData("multiple-with-single-id", 1)]
    [InlineData("missing-tags", 0)]
    [InlineData("multiple-links", 1)]
    [InlineData("wrong-wikidata-host", 1)]
    [InlineData("invalid-json", 1)]
    [InlineData("wrong-mbid", 2)]
    [InlineData("no-image", 2)]
    [InlineData("missing", 3)]
    [InlineData("license-missing", 3)]
    [InlineData("CC BY-SA", 3)]
    [InlineData("conflicting-license", 3)]
    [InlineData("ambiguous-license", 3)]
    [InlineData("no-author", 3)]
    [InlineData("wrong-image-host", 3)]
    [InlineData("wrong-file-page", 3)]
    [InlineData("redirect", 4)]
    [InlineData("invalid-image", 4)]
    [InlineData("large-image", 4)]
    public async Task UnverifiedArtistArtworkIsNeverPublished(string mode, int calls)
    {
        using TestDirectory directory = new();
        using var provider = new Provider([], mode);
        using var online = Connect(new(directory.Path), provider);
        AudioArtistIdentity identity = mode switch
        {
            "ambiguous" or "truncated" => Artist with { ArtistId = null },
            "various" => new("Various Artists", ArtistId),
            "multiple" => new("Artist A; Artist B", null),
            "multiple-with-single-id" => new("Example Artist & Other Artist", ArtistId),
            "missing-tags" => new(null, null),
            _ => Artist
        };
        Assert.Null(await online.FindArtistAsync(identity, true, Token));
        Assert.Null(await online.FindArtistAsync(identity, false, Token));
        Assert.Equal(calls, provider.Requests);
        Assert.Empty(Directory.GetFiles(directory.Path, "*.png"));
    }

    static OnlineAudioArtwork Connect(AudioArtworkCache cache, Provider handler) => new(cache, handler,
        OnlineAudioArtwork.IsAllowedUri, new("https://musicbrainz.org/ws/2/"), new("https://coverartarchive.org/"),
        TimeSpan.Zero, TimeSpan.FromSeconds(5));

    sealed class Provider(byte[] image, string mode) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++;
            Uri uri = request.RequestUri!;
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            if (uri.IdnHost == "musicbrainz.org")
                response.Content = MusicBrainz(uri);
            else if (uri.IdnHost == "www.wikidata.org")
            {
                string mbid = mode == "wrong-mbid" ? Guid.Empty.ToString() : ArtistId;
                var claims = new JsonObject { ["P434"] = Claim(mbid) };
                if (mode != "no-image")
                    claims["P18"] = Claim("Portrait.png");
                response.Content = Json(new { entities = new { Q42 = new { id = "Q42", claims } } });
            }
            else if (uri.IdnHost == "commons.wikimedia.org")
            {
                Assert.Contains("titles=File%3APortrait.png", uri.Query);
                if (mode == "missing")
                    response.StatusCode = HttpStatusCode.NotFound;
                else
                    response.Content = Commons();
            }
            else if (uri.AbsoluteUri == ImageUrl)
            {
                if (mode == "redirect")
                {
                    response.StatusCode = HttpStatusCode.TemporaryRedirect;
                    response.Headers.Location = new Uri("https://upload.wikimedia.org/wikipedia/commons/other.png");
                }
                response.Content = new ByteArrayContent(mode switch
                {
                    "large-image" => new byte[AudioArtworkImage.MaxBytes + 1],
                    "invalid-image" => Encoding.UTF8.GetBytes("invalid image"),
                    _ => image
                });
            }
            else
                Assert.Fail($"Unexpected request to {uri}");
            return Task.FromResult(response);
        }

        HttpContent MusicBrainz(Uri uri)
        {
            if (mode == "invalid-json")
                return new StringContent("{{ invalid");
            if (uri.AbsolutePath.EndsWith("/artist", StringComparison.Ordinal))
            {
                var first = new { id = ArtistId, name = Artist.Name };
                var second = new { id = Guid.NewGuid().ToString(), name = Artist.Name };
                return Json(new { count = mode is "ambiguous" or "truncated" ? 2 : 1,
                    artists = mode == "ambiguous" ? new[] { first, second } : [first] });
            }
            Assert.EndsWith("/artist/" + ArtistId, uri.AbsolutePath);
            string url = mode == "wrong-wikidata-host" ? "https://www.wikidata.org.invalid/wiki/Q42" : "https://www.wikidata.org/wiki/Q42";
            var link = new { type = "wikidata", ended = false, url = new { resource = url } };
            return Json(new { id = ArtistId, name = Artist.Name, relations = mode == "multiple-links"
                ? new[] { link, link with { url = new { resource = "https://www.wikidata.org/wiki/Q43" } } }
                : [link] });
        }

        HttpContent Commons()
        {
            var metadata = new JsonObject
            {
                ["Copyrighted"] = Field("False"),
                ["AttributionRequired"] = Field("false")
            };
            if (mode != "no-author")
                metadata["Artist"] = Field("<a href='/wiki/User:Example'>Example photographer</a>");
            if (mode != "license-missing")
                metadata["LicenseShortName"] = Field(mode switch
                {
                    "CC0" => "CC0",
                    "CC BY-SA" => "CC BY-SA 4.0",
                    "ambiguous-license" => "CC0 / CC BY-SA",
                    _ => "Public domain"
                });
            if (mode is "CC0" or "conflicting-license")
                metadata["LicenseUrl"] = Field(mode == "CC0"
                    ? "https://creativecommons.org/publicdomain/zero/1.0/" : "https://creativecommons.org/licenses/by-sa/4.0/");
            return Json(new { query = new { pages = new[] { new { imageinfo = new[] { new
            {
                canonicaltitle = "File:Portrait.png", mime = "image/png",
                url = mode == "wrong-image-host" ? ImageUrl.Replace("upload.wikimedia.org", "upload.wikimedia.org.invalid") : ImageUrl,
                descriptionurl = "https://commons.wikimedia.org/wiki/File:" + (mode == "wrong-file-page" ? "Other.png" : "Portrait.png"),
                extmetadata = metadata
            } } } } } });
        }

        static JsonArray Claim(string value) => new(new JsonObject
        {
            ["rank"] = "normal",
            ["mainsnak"] = new JsonObject { ["snaktype"] = "value", ["datavalue"] = Field(value) }
        });

        static JsonObject Field(string value) => new() { ["value"] = value };
        static StringContent Json<T>(T value) => new(System.Text.Json.JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
    }
}
