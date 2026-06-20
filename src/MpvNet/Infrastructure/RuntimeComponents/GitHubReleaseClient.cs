using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MpvNet;

internal static class GitHubReleaseClient
{
    static readonly TimeSpan ReleaseRequestTimeout = TimeSpan.FromSeconds(30);
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<GitHubRelease> GetReleaseAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("mpv.net", AppInfo.Version.ToString()));
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(ReleaseRequestTimeout);

        try
        {
            using var response = await Http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<GitHubRelease>(json, JsonOptions)
                ?? throw new InvalidOperationException("Invalid GitHub release payload.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutSource.IsCancellationRequested)
        {
            Log.Error($"Timed out while reading GitHub release metadata after {ReleaseRequestTimeout.TotalSeconds:0}s. url='{Log.SafeValue(url)}'");
            throw;
        }
    }

    public static async Task DownloadAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Http.GetAsync(
                url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var output = File.Create(destinationPath);
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            RuntimeComponentFileSystem.DeleteIfExists(destinationPath);
            throw;
        }
    }
}
