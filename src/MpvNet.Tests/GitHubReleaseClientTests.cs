using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using MpvNet;

using Xunit;

namespace MpvNet.Tests;

public sealed class GitHubReleaseClientTests
{
    const string AssetUrl = "https://github.com/example/tool/releases/download/v1/tool.exe?token=secret";

    [Fact]
    public async Task SuccessfulDownloadPreservesTheCompleteFile()
    {
        using TestDirectory directory = new();
        string destination = Path.Combine(directory.Path, "tool.download");
        using HttpClient http = CreateClient(new MemoryStream([1, 2, 3]));

        long bytes = await GitHubReleaseClient.DownloadAsync(
            AssetUrl, destination, CancellationToken.None, http, TimeSpan.FromSeconds(10));

        Assert.Equal(3, bytes);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(destination));
        using FileStream exclusive = File.Open(destination, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public async Task ApplicationCancellationRemovesPartialFileWithoutLoggingAnError()
    {
        using TestDirectory directory = new();
        using CancellationTokenSource cancellation = new();
        string destination = Path.Combine(directory.Path, "tool.download");
        using HttpClient http = CreateClient(new InterruptedStream(token =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return ValueTask.FromResult(0);
        }));
        int logOffset = ReadLog().Length;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GitHubReleaseClient.DownloadAsync(
            AssetUrl, destination, cancellation.Token, http, TimeSpan.FromSeconds(10)));

        Assert.False(File.Exists(destination));
        Assert.DoesNotContain("[ERROR]", ReadLog()[logOffset..]);
    }

    [Fact]
    public async Task AlreadyCanceledDownloadDoesNotCreateAFileOrLogAnError()
    {
        using TestDirectory directory = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        string destination = Path.Combine(directory.Path, "tool.download");
        using HttpClient http = CreateClient(new MemoryStream([1, 2, 3]));
        int logOffset = ReadLog().Length;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GitHubReleaseClient.DownloadAsync(
            AssetUrl, destination, cancellation.Token, http, TimeSpan.FromSeconds(10)));

        Assert.False(File.Exists(destination));
        Assert.DoesNotContain("[ERROR]", ReadLog()[logOffset..]);
    }

    [Fact]
    public async Task TimeoutRemovesPartialFileAndReportsTheLimitAndProgress()
    {
        using TestDirectory directory = new();
        string destination = Path.Combine(directory.Path, "tool.download");
        using HttpClient http = CreateClient(new InterruptedStream(async token =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return 0;
        }));
        int logOffset = ReadLog().Length;

        TimeoutException exception = await Assert.ThrowsAsync<TimeoutException>(() => GitHubReleaseClient.DownloadAsync(
            AssetUrl, destination, CancellationToken.None, http, TimeSpan.FromMilliseconds(250)));

        Assert.False(File.Exists(destination));
        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
        Assert.Contains("after 0.25s", exception.Message);
        Assert.Contains("bytes=3", exception.Message);
        string logged = ReadLog()[logOffset..];
        Assert.Contains("[ERROR] Timed out while downloading runtime component asset", logged);
        Assert.DoesNotContain("secret", logged);
    }

    [Fact]
    public async Task TransportFailureStillLogsAnErrorAndRemovesPartialFile()
    {
        using TestDirectory directory = new();
        string destination = Path.Combine(directory.Path, "tool.download");
        using HttpClient http = CreateClient(new InterruptedStream(_ => throw new IOException("transport failed")));
        int logOffset = ReadLog().Length;

        await Assert.ThrowsAsync<IOException>(() => GitHubReleaseClient.DownloadAsync(
            AssetUrl, destination, CancellationToken.None, http, TimeSpan.FromSeconds(10)));

        Assert.False(File.Exists(destination));
        Assert.Contains("[ERROR] Failed to download runtime component asset", ReadLog()[logOffset..]);
    }

    [Fact]
    public async Task UnexpectedCancellationStillLogsAnError()
    {
        using TestDirectory directory = new();
        string destination = Path.Combine(directory.Path, "tool.download");
        using HttpClient http = CreateClient(new InterruptedStream(_ => throw new TaskCanceledException("transport canceled")));
        int logOffset = ReadLog().Length;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GitHubReleaseClient.DownloadAsync(
            AssetUrl, destination, CancellationToken.None, http, TimeSpan.FromSeconds(10)));

        Assert.False(File.Exists(destination));
        Assert.Contains("[ERROR] Failed to download runtime component asset", ReadLog()[logOffset..]);
    }

    [Fact]
    public async Task RedirectToAnUnapprovedHostIsRejected()
    {
        using TestDirectory directory = new();
        string destination = Path.Combine(directory.Path, "tool.download");
        using HttpClient http = new(new ResponseHandler(() => new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri("https://example.com/tool.exe") }
        }));

        await Assert.ThrowsAsync<InvalidOperationException>(() => GitHubReleaseClient.DownloadAsync(
            AssetUrl, destination, CancellationToken.None, http, TimeSpan.FromSeconds(10)));

        Assert.False(File.Exists(destination));
    }

    [Fact]
    public async Task ResumeAppendsOnlyTheRequestedSuffixThroughRedirects()
    {
        using TestDirectory directory = new();
        string destination = Path.Combine(directory.Path, "tool.partial");
        File.WriteAllBytes(destination, [1, 2, 3]);
        int calls = 0;
        using HttpClient http = new(new RequestHandler(request =>
        {
            Assert.Equal(3, Assert.Single(request.Headers.Range!.Ranges).From);
            if (++calls == 1)
                return new HttpResponseMessage(HttpStatusCode.Redirect)
                { Headers = { Location = new Uri("https://release-assets.githubusercontent.com/tool.exe") } };
            return PartialResponse([4, 5, 6], 3, 5, 6);
        }));

        long bytes = await GitHubReleaseClient.DownloadAsync(
            AssetUrl, destination, CancellationToken.None, http, TimeSpan.FromSeconds(10), expectedLength: 6);

        Assert.Equal(6, bytes);
        Assert.Equal(2, calls);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, File.ReadAllBytes(destination));
    }

    [Fact]
    public async Task ServerIgnoringRangeReplacesThePartialInsteadOfAppending()
    {
        using TestDirectory directory = new();
        string destination = Path.Combine(directory.Path, "tool.partial");
        File.WriteAllBytes(destination, [9, 9, 9]);
        using HttpClient http = CreateClient(new MemoryStream([1, 2, 3, 4, 5, 6]));

        await GitHubReleaseClient.DownloadAsync(
            AssetUrl, destination, CancellationToken.None, http, TimeSpan.FromSeconds(10), expectedLength: 6);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, File.ReadAllBytes(destination));
    }

    [Theory]
    [InlineData(2, 5, 6)]
    [InlineData(3, 6, 7)]
    [InlineData(3, 4, 6)]
    public async Task IncompatibleRangeDiscardsThePartial(long from, long to, long length)
    {
        using TestDirectory directory = new();
        string destination = Path.Combine(directory.Path, "tool.partial");
        File.WriteAllBytes(destination, [1, 2, 3]);
        using HttpClient http = new(new RequestHandler(_ => PartialResponse([4, 5, 6], from, to, length)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => GitHubReleaseClient.DownloadAsync(
            AssetUrl, destination, CancellationToken.None, http, TimeSpan.FromSeconds(10), expectedLength: 6));

        Assert.False(File.Exists(destination));
    }

    [Fact]
    public async Task ClosingTheApplicationKeepsThePartialForTheNextRun()
    {
        using TestDirectory directory = new();
        using CancellationTokenSource cancellation = new();
        string destination = Path.Combine(directory.Path, "tool.partial");
        using HttpClient http = CreateClient(new InterruptedStream(token =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return ValueTask.FromResult(0);
        }), contentLength: 6);
        int logOffset = ReadLog().Length;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GitHubReleaseClient.DownloadAsync(
            AssetUrl, destination, cancellation.Token, http, TimeSpan.FromSeconds(10), expectedLength: 6));

        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(destination));
        Assert.DoesNotContain("[ERROR]", ReadLog()[logOffset..]);
        using HttpClient resumed = new(new RequestHandler(_ => PartialResponse([4, 5, 6], 3, 5, 6)));
        await GitHubReleaseClient.DownloadAsync(
            AssetUrl, destination, CancellationToken.None, resumed, TimeSpan.FromSeconds(10), expectedLength: 6);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, File.ReadAllBytes(destination));
    }

    [Fact]
    public async Task InactivityTimeoutKeepsProgressAndIdentifiesTheStall()
    {
        using TestDirectory directory = new();
        string destination = Path.Combine(directory.Path, "tool.partial");
        using HttpClient http = CreateClient(new InterruptedStream(async token =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return 0;
        }), contentLength: 6);

        TimeoutException error = await Assert.ThrowsAsync<TimeoutException>(() => GitHubReleaseClient.DownloadAsync(
            AssetUrl, destination, CancellationToken.None, http, TimeSpan.FromSeconds(10),
            expectedLength: 6, inactivityTimeout: TimeSpan.FromMilliseconds(100)));

        Assert.Contains("no progress", error.Message);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(destination));
    }

    [Fact]
    public async Task SlowTransferContinuesWhileBytesKeepArriving()
    {
        using TestDirectory directory = new();
        string destination = Path.Combine(directory.Path, "tool.partial");
        using HttpClient http = CreateClient(new SlowProgressStream());

        long bytes = await GitHubReleaseClient.DownloadAsync(
            AssetUrl, destination, CancellationToken.None, http, TimeSpan.FromSeconds(10),
            expectedLength: 6, inactivityTimeout: TimeSpan.FromMilliseconds(300));

        Assert.Equal(6, bytes);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, File.ReadAllBytes(destination));
    }

    [Fact]
    public async Task RetryAfterTransportFailureContinuesFromWrittenBytes()
    {
        using TestDirectory directory = new();
        string destination = Path.Combine(directory.Path, "tool.partial");
        int calls = 0;
        using HttpClient http = new(new RequestHandler(request =>
        {
            if (++calls == 1)
                return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StreamContent(new InterruptedStream(_ => throw new IOException("connection lost")))
                    { Headers = { ContentLength = 6 } } };
            Assert.Equal(3, Assert.Single(request.Headers.Range!.Ranges).From);
            return PartialResponse([4, 5, 6], 3, 5, 6);
        }));

        await GitHubReleaseClient.DownloadAsync(
            AssetUrl, destination, CancellationToken.None, http, TimeSpan.FromSeconds(10),
            expectedLength: 6, maximumAttempts: 2);

        Assert.Equal(2, calls);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, File.ReadAllBytes(destination));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task RateLimitKeepsThePartialWithoutImmediateRetries(HttpStatusCode status)
    {
        using TestDirectory directory = new();
        string destination = Path.Combine(directory.Path, "tool.partial");
        File.WriteAllBytes(destination, [1, 2, 3]);
        int calls = 0;
        using HttpClient http = new(new RequestHandler(_ =>
        {
            calls++;
            return new HttpResponseMessage(status) { Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(1)) } };
        }));

        await Assert.ThrowsAsync<HttpRequestException>(() => GitHubReleaseClient.DownloadAsync(
            AssetUrl, destination, CancellationToken.None, http, TimeSpan.FromSeconds(10),
            expectedLength: 6, maximumAttempts: 3));

        Assert.Equal(1, calls);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(destination));
    }

    [Fact]
    public async Task TruncatedResponseRetainsOnlyTheBytesActuallyReceived()
    {
        using TestDirectory directory = new();
        string destination = Path.Combine(directory.Path, "tool.partial");
        using HttpClient http = new(new RequestHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream([1, 2, 3])) };
            response.Content.Headers.ContentLength = 6;
            return response;
        }));

        await Assert.ThrowsAsync<IOException>(() => GitHubReleaseClient.DownloadAsync(
            AssetUrl, destination, CancellationToken.None, http, TimeSpan.FromSeconds(10), expectedLength: 6));

        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(destination));
    }

    [Fact]
    public async Task OversizedPartialRestartsWithoutRange()
    {
        using TestDirectory directory = new();
        string destination = Path.Combine(directory.Path, "tool.partial");
        File.WriteAllBytes(destination, [9, 9, 9, 9]);
        using HttpClient http = new(new RequestHandler(request =>
        {
            Assert.Null(request.Headers.Range);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
        }));

        await GitHubReleaseClient.DownloadAsync(
            AssetUrl, destination, CancellationToken.None, http, TimeSpan.FromSeconds(10), expectedLength: 3);

        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(destination));
    }

    static HttpResponseMessage PartialResponse(byte[] bytes, long from, long to, long length)
    {
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(from, to, length);
        return response;
    }

    sealed class RequestHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response(request));
        }
    }

    sealed class SlowProgressStream() : MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6 })
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(100, cancellationToken);
            return await base.ReadAsync(buffer[..1], cancellationToken);
        }
    }

    static HttpClient CreateClient(Stream stream, long? contentLength = null) => new(new ResponseHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StreamContent(stream) { Headers = { ContentLength = contentLength } }
    }));

    static string ReadLog()
    {
        string path = Path.Combine(Log.LogFolder, $"mpvnet-{DateTime.Now:yyyy-MM-dd}.log");
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    sealed class ResponseHandler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response());
        }
    }

    sealed class InterruptedStream(Func<CancellationToken, ValueTask<int>> interrupt) : MemoryStream(new byte[] { 1, 2, 3 })
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Position < Length ? base.ReadAsync(buffer, cancellationToken) : interrupt(cancellationToken);
    }
}
