using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MpvNet;

internal static class GitHubReleaseClient
{
    const long MaximumDownloadBytes = 512L * 1024 * 1024;
    const int MaximumRedirects = 5;
    static readonly TimeSpan ReleaseRequestTimeout = TimeSpan.FromSeconds(30);
    static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);
    static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "api.github.com",
        "github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com"
    };
    static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<GitHubRelease> GetReleaseAsync(string url, CancellationToken cancellationToken)
    {
        ValidateGitHubUri(url);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("mpv.net", AppInfo.Version.ToString()));
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(ReleaseRequestTimeout);

        try
        {
            using HttpResponseMessage response = await Http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(timeoutSource.Token).ConfigureAwait(false);
            return JsonSerializer.Deserialize<GitHubRelease>(json, JsonOptions)
                ?? throw new InvalidOperationException("Invalid GitHub release payload.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutSource.IsCancellationRequested)
        {
            Log.Error($"Timed out while reading GitHub release metadata after {ReleaseRequestTimeout.TotalSeconds:0}s. url='{Log.SafeValue(url)}'");
            throw;
        }
    }

    public static async Task<long> DownloadAsync(string url, string destinationPath, CancellationToken cancellationToken)
    {
        ValidateGitHubUri(url);
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(DownloadTimeout);
            using HttpResponseMessage response = await SendWithValidatedRedirectsAsync(url, timeoutSource.Token)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is long length &&
                (length <= 0 || length > MaximumDownloadBytes))
            {
                throw new InvalidOperationException($"Runtime component download has an invalid content length: {length}.");
            }

            await using var input = await response.Content.ReadAsStreamAsync(timeoutSource.Token).ConfigureAwait(false);
            await using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            byte[] buffer = new byte[81920];
            long total = 0;
            while (true)
            {
                int read = await input.ReadAsync(buffer, timeoutSource.Token).ConfigureAwait(false);
                if (read == 0)
                    break;

                total += read;
                if (total > MaximumDownloadBytes)
                    throw new InvalidOperationException($"Runtime component download exceeds the {MaximumDownloadBytes} byte limit.");

                await output.WriteAsync(buffer.AsMemory(0, read), timeoutSource.Token).ConfigureAwait(false);
            }

            if (total == 0)
                throw new InvalidOperationException("Runtime component download is empty.");

            return total;
        }
        catch (Exception ex)
        {
            Log.Error(
                ex,
                $"Failed to download runtime component asset. url='{Log.SafeValue(url)}', destination='{Log.SafeValue(destinationPath)}'");
            RuntimeComponentFileSystem.DeleteIfExists(destinationPath);
            throw;
        }
    }

    static async Task<HttpResponseMessage> SendWithValidatedRedirectsAsync(string url, CancellationToken cancellationToken)
    {
        Uri current = new(url, UriKind.Absolute);
        for (int redirectCount = 0; ; redirectCount++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("mpv.net", AppInfo.Version.ToString()));
            HttpResponseMessage response = await Http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (!IsRedirect(response.StatusCode))
                return response;

            if (redirectCount >= MaximumRedirects || response.Headers.Location is null)
            {
                response.Dispose();
                throw new InvalidOperationException("Runtime component download has an unexpected redirect.");
            }

            Uri next = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(current, response.Headers.Location);
            response.Dispose();
            ValidateGitHubUri(next.AbsoluteUri);
            current = next;
        }
    }

    static bool IsRedirect(System.Net.HttpStatusCode statusCode) =>
        statusCode is System.Net.HttpStatusCode.Moved or System.Net.HttpStatusCode.Redirect or
            System.Net.HttpStatusCode.RedirectMethod or System.Net.HttpStatusCode.TemporaryRedirect or
            System.Net.HttpStatusCode.PermanentRedirect;

    static void ValidateGitHubUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !AllowedHosts.Contains(uri.Host))
        {
            throw new InvalidOperationException("Runtime component download URL is not an allowed HTTPS GitHub endpoint.");
        }
    }
}
