using System;
using System.IO;
using System.Net;
using System.Net.Http;
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

    static HttpClient CreateClient(Stream stream) => new(new ResponseHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StreamContent(stream)
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
