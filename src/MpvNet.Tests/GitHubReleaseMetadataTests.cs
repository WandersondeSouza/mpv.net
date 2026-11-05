using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using MpvNet;
using Xunit;

namespace MpvNet.Tests;

public sealed class GitHubReleaseMetadataTests
{
    const string ReleaseUrl = "https://api.github.com/repos/example/tool/releases/latest";
    static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MetadataMapsThePublishedAssetContractAndKeepsTheUserAgent()
    {
        using HttpClient http = CreateHttp(request =>
        {
            Assert.Equal(ReleaseUrl, request.RequestUri!.AbsoluteUri);
            Assert.Contains("mpv.net/", request.Headers.UserAgent.ToString());
            Assert.Null(request.Headers.Range);
            return Json("""
                {"assets":[{"name":"tool.exe","browser_download_url":"https://github.com/example/tool.exe",
                "digest":"sha256:abc","size":123,"future_field":true}],"other_field":[]}
                """);
        });
        var client = new GitHubReleaseMetadataClient(http, TimeSpan.FromSeconds(5));
        GitHubAsset asset = Assert.Single((await client.GetReleaseAsync(ReleaseUrl, Token)).Assets);
        Assert.Equal("tool.exe", asset.Name);
        Assert.Equal("https://github.com/example/tool.exe", asset.BrowserDownloadUrl);
        Assert.Equal("sha256:abc", asset.Digest);
        Assert.Equal(123, asset.Size);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"assets\":{}}")]
    [InlineData("{\"assets\":[{\"size\":\"bad\"}]}")]
    public async Task MalformedOrMissingContractIsRejected(string json)
    {
        using HttpClient http = CreateHttp(_ => Json(json));
        var client = new GitHubReleaseMetadataClient(http, TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<JsonException>(() => client.GetReleaseAsync(ReleaseUrl, Token));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"assets\":null}")]
    [InlineData("{\"assets\":[null]}")]
    public async Task NullContractEntriesAreRejected(string json)
    {
        using HttpClient http = CreateHttp(_ => Json(json));
        var client = new GitHubReleaseMetadataClient(http, TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetReleaseAsync(ReleaseUrl, Token));
    }

    [Theory]
    [InlineData("http://api.github.com/repos/example/tool/releases/latest")]
    [InlineData("https://example.com/releases/latest")]
    public async Task UnexpectedInitialDestinationsNeverReachTheTransport(string url)
    {
        int calls = 0;
        using HttpClient http = CreateHttp(_ => { calls++; return Json(); });
        var client = new GitHubReleaseMetadataClient(http, TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetReleaseAsync(url, Token));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task MetadataFollowsOnlyValidatedRedirects()
    {
        int calls = 0;
        using HttpClient http = CreateHttp(request =>
        {
            calls++;
            Assert.Contains("mpv.net/", request.Headers.UserAgent.ToString());
            return calls == 1 ? Redirect("/repos/example/renamed/releases/latest") : Json();
        });
        var client = new GitHubReleaseMetadataClient(http, TimeSpan.FromSeconds(5));
        Assert.Empty((await client.GetReleaseAsync(ReleaseUrl, Token)).Assets);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData("https://example.com/payload")]
    [InlineData("http://api.github.com/payload")]
    public async Task UnsafeMetadataRedirectIsRejectedBeforeAnotherRequest(string location)
    {
        int calls = 0;
        using HttpClient http = CreateHttp(_ => { calls++; return Redirect(location); });
        var client = new GitHubReleaseMetadataClient(http, TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetReleaseAsync(ReleaseUrl, Token));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task MetadataRedirectLoopsAreBounded()
    {
        int calls = 0;
        using HttpClient http = CreateHttp(_ => { calls++; return Redirect(ReleaseUrl); });
        var client = new GitHubReleaseMetadataClient(http, TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetReleaseAsync(ReleaseUrl, Token));
        Assert.Equal(6, calls);
    }

    [Fact]
    public async Task DeclaredOversizedResponseIsRejectedWithoutReadingItsBody()
    {
        using var body = new ForwardOnlyStream([1]);
        using HttpClient http = CreateHttp(_ => new(HttpStatusCode.OK)
        {
            Content = new StreamContent(body)
            {
                Headers = { ContentLength = GitHubReleaseMetadataClient.MaximumResponseBytes + 1 }
            }
        });
        var client = new GitHubReleaseMetadataClient(http, TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetReleaseAsync(ReleaseUrl, Token));
        Assert.Equal(0, body.BytesRead);
        Assert.True(body.Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StreamLimitIsEnforcedWithMissingOrIncorrectContentLength(bool incorrectLength)
    {
        using var body = new ForwardOnlyStream(new byte[GitHubReleaseMetadataClient.MaximumResponseBytes + 1]);
        using HttpClient http = CreateHttp(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) };
            if (incorrectLength)
                response.Content.Headers.ContentLength = 1;
            return response;
        });
        var client = new GitHubReleaseMetadataClient(http, TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetReleaseAsync(ReleaseUrl, Token));
        Assert.True(body.Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeoutAndCallerCancellationInterruptTheBodyAndReleaseTheGate(bool callerCancellation)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        int calls = 0;
        using HttpClient http = CreateHttp(_ => ++calls == 1
            ? new(HttpStatusCode.OK) { Content = new StreamContent(new StalledStream()) } : Json());
        var client = new GitHubReleaseMetadataClient(http,
            callerCancellation ? TimeSpan.FromSeconds(5) : TimeSpan.FromMilliseconds(100));
        if (callerCancellation)
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetReleaseAsync(ReleaseUrl, cancellation.Token));
        Assert.Equal(callerCancellation, cancellation.IsCancellationRequested);
        Assert.Empty((await client.GetReleaseAsync(ReleaseUrl, Token)).Assets);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task AlreadyCanceledMetadataRequestDoesNotReachTheTransport()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        int calls = 0;
        using HttpClient http = CreateHttp(_ => { calls++; return Json(); });
        var client = new GitHubReleaseMetadataClient(http, TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetReleaseAsync(ReleaseUrl, cancellation.Token));
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "delta")]
    [InlineData(HttpStatusCode.Forbidden, "date")]
    [InlineData(HttpStatusCode.Forbidden, "reset")]
    [InlineData(HttpStatusCode.Forbidden, "both")]
    [InlineData(HttpStatusCode.TooManyRequests, "fallback")]
    [InlineData(HttpStatusCode.Forbidden, "invalid-reset")]
    [InlineData(HttpStatusCode.Forbidden, "past-reset")]
    public async Task RateLimitDefersOtherRepositoriesUntilTheServerDeadline(HttpStatusCode status, string headers)
    {
        var clock = new ManualClock();
        int calls = 0;
        using HttpClient http = CreateHttp(_ =>
        {
            if (++calls > 1)
                return Json();
            var response = new HttpResponseMessage(status);
            if (headers is "delta" or "both")
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            if (headers == "date")
                response.Headers.RetryAfter = new RetryConditionHeaderValue(clock.GetUtcNow().AddMinutes(1));
            if (headers is "reset" or "both" or "invalid-reset" or "past-reset")
            {
                response.Headers.Add("x-ratelimit-remaining", "0");
                response.Headers.Add("x-ratelimit-reset", headers == "invalid-reset" ? "9223372036854775807" :
                    clock.GetUtcNow().AddMinutes(headers == "past-reset" ? -1 : 1).ToUnixTimeSeconds().ToString());
            }
            return response;
        });
        var client = new GitHubReleaseMetadataClient(http, TimeSpan.FromSeconds(5), clock);
        HttpRequestException first = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetReleaseAsync(ReleaseUrl, Token));
        Assert.Equal(status, first.StatusCode);
        string anotherRepository = ReleaseUrl.Replace("/tool/", "/another/");
        clock.Advance(TimeSpan.FromSeconds(headers == "delta" ? 29 : 59));
        HttpRequestException paused = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetReleaseAsync(anotherRepository, Token));
        Assert.Equal(HttpStatusCode.TooManyRequests, paused.StatusCode);
        Assert.Equal(1, calls);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Empty((await client.GetReleaseAsync(anotherRepository, Token)).Assets);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task SuccessfulLastAllowedRequestAlsoDefersTheNextQuery()
    {
        var clock = new ManualClock();
        int calls = 0;
        using HttpClient http = CreateHttp(_ =>
        {
            calls++;
            HttpResponseMessage response = Json();
            response.Headers.Add("x-ratelimit-remaining", "0");
            response.Headers.Add("x-ratelimit-reset", clock.GetUtcNow().AddMinutes(1).ToUnixTimeSeconds().ToString());
            return response;
        });
        var client = new GitHubReleaseMetadataClient(http, TimeSpan.FromSeconds(5), clock);
        Assert.Empty((await client.GetReleaseAsync(ReleaseUrl, Token)).Assets);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetReleaseAsync(ReleaseUrl, Token));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task OtherHttpErrorsAreNotRetriedAndDoNotCreateARateLimitCooldown()
    {
        int calls = 0;
        using HttpClient http = CreateHttp(_ => ++calls == 1 ? new(HttpStatusCode.ServiceUnavailable) : Json());
        var client = new GitHubReleaseMetadataClient(http, TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetReleaseAsync(ReleaseUrl, Token));
        Assert.Equal(1, calls);
        Assert.Empty((await client.GetReleaseAsync(ReleaseUrl, Token)).Assets);
        Assert.Equal(2, calls);
    }

    static HttpResponseMessage Json(string json = "{\"assets\":[]}") => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    static HttpResponseMessage Redirect(string location) => new(HttpStatusCode.Redirect)
    {
        Headers = { Location = new Uri(location, UriKind.RelativeOrAbsolute) }
    };

    static HttpClient CreateHttp(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new Handler(respond)) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

    sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request));
        }
    }

    sealed class ManualClock : TimeProvider
    {
        DateTimeOffset _now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }

    sealed class ForwardOnlyStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
        public int BytesRead { get; private set; }
        public bool Disposed { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int count = await base.ReadAsync(buffer, cancellationToken);
            BytesRead += count;
            return count;
        }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    sealed class StalledStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
}
