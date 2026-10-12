using System.Diagnostics;
using System.Net;
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
    static readonly TimeSpan ResumableDownloadTimeout = TimeSpan.FromHours(2);
    static readonly TimeSpan DownloadInactivityTimeout = TimeSpan.FromSeconds(60);
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

    public static Task<long> DownloadAsync(string url, string destinationPath, CancellationToken cancellationToken) =>
        DownloadAsync(url, destinationPath, cancellationToken, Http, DownloadTimeout);

    public static Task<long> DownloadResumableAsync(
        string url, string destinationPath, long expectedLength, CancellationToken cancellationToken) =>
        DownloadAsync(url, destinationPath, cancellationToken, Http, ResumableDownloadTimeout,
            expectedLength, DownloadInactivityTimeout, maximumAttempts: 3);

    internal static async Task<long> DownloadAsync(
        string url,
        string destinationPath,
        CancellationToken cancellationToken,
        HttpClient http,
        TimeSpan downloadTimeout,
        long? expectedLength = null,
        TimeSpan? inactivityTimeout = null,
        int maximumAttempts = 1)
    {
        ValidateGitHubUri(url);
        if (expectedLength is <= 0 or > MaximumDownloadBytes)
            throw new InvalidOperationException("Runtime component asset size is invalid.");
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(downloadTimeout);
        bool preservePartial = expectedLength.HasValue;
        try
        {
            for (int attempt = 1; ; attempt++)
            {
                timeoutSource.Token.ThrowIfCancellationRequested();
                try
                {
                    return await DownloadAttemptAsync(http, url, destinationPath, expectedLength,
                        inactivityTimeout, timeoutSource.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (attempt < maximumAttempts &&
                    !timeoutSource.IsCancellationRequested && IsRetryable(ex))
                {
                    Log.Debug($"Retrying runtime component download. attempt={attempt}, bytes={GetPartialLength(destinationPath)}, reason='{Log.SafeValue(ex.Message)}'");
                    await Task.Delay(TimeSpan.FromSeconds(attempt), timeoutSource.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Log.Debug($"Runtime component download canceled by the application. url='{Log.SafeValue(url)}', bytes={GetPartialLength(destinationPath)}, partialRetained={preservePartial}");
            if (!preservePartial)
                RuntimeComponentFileSystem.DeleteIfExists(destinationPath);
            throw;
        }
        catch (OperationCanceledException ex) when (timeoutSource.IsCancellationRequested)
        {
            string message = FormattableString.Invariant($"Timed out while downloading runtime component asset after {downloadTimeout.TotalSeconds:0.###}s. url='{Log.SafeValue(url)}', destination='{Log.SafeValue(destinationPath)}', bytes={GetPartialLength(destinationPath)}, partialRetained={preservePartial}");
            Log.Error(message);
            if (!preservePartial)
                RuntimeComponentFileSystem.DeleteIfExists(destinationPath);
            throw new TimeoutException(message, ex);
        }
        catch (Exception ex)
        {
            bool retain = preservePartial && IsTransient(ex);
            Log.Error(
                ex,
                $"Failed to download runtime component asset. url='{Log.SafeValue(url)}', destination='{Log.SafeValue(destinationPath)}', bytes={GetPartialLength(destinationPath)}, partialRetained={retain}");
            if (!retain)
                RuntimeComponentFileSystem.DeleteIfExists(destinationPath);
            throw;
        }
    }

    static long GetPartialLength(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

    static bool IsTransient(Exception ex) => ex is IOException or TimeoutException ||
        ex is HttpRequestException { StatusCode: null or HttpStatusCode.RequestTimeout or HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests } ||
        ex is HttpRequestException httpError && (int?)httpError.StatusCode >= 500;

    static bool IsRetryable(Exception ex) => IsTransient(ex) &&
        ex is not HttpRequestException { StatusCode: HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests };

    static async Task<long> DownloadAttemptAsync(
        HttpClient http, string url, string destinationPath, long? expectedLength,
        TimeSpan? inactivityTimeout, CancellationToken cancellationToken)
    {
        long offset = expectedLength.HasValue ? GetPartialLength(destinationPath) : 0;
        if (expectedLength.HasValue && offset > expectedLength.Value)
        {
            RuntimeComponentFileSystem.DeleteIfExists(destinationPath);
            offset = 0;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (expectedLength.HasValue && offset == expectedLength.Value)
            return offset; // The caller must still verify the complete asset SHA-256.

        using var inactivity = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        void ResetInactivity()
        {
            if (inactivityTimeout.HasValue)
                inactivity.CancelAfter(inactivityTimeout.Value);
        }
        ResetInactivity();
        Stopwatch elapsed = Stopwatch.StartNew();
        try
        {
            using HttpResponseMessage response = await SendWithValidatedRedirectsAsync(
                http, url, inactivity.Token, offset).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                // Respect server throttling instead of issuing immediate retries.
                Log.Debug($"GitHub refused the download with HTTP {(int)response.StatusCode}; retryAfter='{response.Headers.RetryAfter}', rateLimitReset='{string.Join(",", response.Headers.TryGetValues("x-ratelimit-reset", out var reset) ? reset : [])}'.");
            }
            response.EnsureSuccessStatusCode();
            if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                ContentRangeHeaderValue? range = response.Content.Headers.ContentRange;
                if (range is null || range.Unit != "bytes" || range.From != offset ||
                    !range.HasLength || range.Length <= offset || range.Length > MaximumDownloadBytes ||
                    range.To != range.Length - 1 ||
                    expectedLength.HasValue && range.Length != expectedLength)
                    throw new InvalidOperationException("Runtime component download returned an incompatible byte range.");
            }
            else if (response.StatusCode == HttpStatusCode.OK)
            {
                offset = 0; // A server ignoring Range must replace, never append.
            }
            else
                throw new InvalidOperationException("Runtime component download returned an unexpected HTTP status.");

            long? contentLength = response.Content.Headers.ContentLength;
            if (contentLength is <= 0 or > MaximumDownloadBytes ||
                expectedLength.HasValue && contentLength.HasValue && contentLength != expectedLength.Value - offset)
                throw new InvalidOperationException("Runtime component download has an invalid content length.");

            ResetInactivity();
            await using var input = await response.Content.ReadAsStreamAsync(inactivity.Token).ConfigureAwait(false);
            await using var output = new FileStream(destinationPath, new FileStreamOptions
            {
                Mode = expectedLength.HasValue ? (offset > 0 ? FileMode.Open : FileMode.Create) : FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 81920,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            });
            output.Position = offset;
            long total = offset;
            double lastProgressSeconds = 0;
            Log.Debug($"Runtime component transfer started. url='{Log.SafeValue(url)}', resumedBytes={offset}, expectedBytes={expectedLength}, headersSeconds={elapsed.Elapsed.TotalSeconds:0.0}");
            byte[] buffer = new byte[81920];
            while (true)
            {
                ResetInactivity();
                int read = await input.ReadAsync(buffer, inactivity.Token).ConfigureAwait(false);
                if (read == 0)
                    break;
                if (total + read > (expectedLength ?? MaximumDownloadBytes))
                    throw new InvalidOperationException("Runtime component download exceeds its expected size or safety limit.");
                await output.WriteAsync(buffer.AsMemory(0, read), inactivity.Token).ConfigureAwait(false);
                total += read;
                if (elapsed.Elapsed.TotalSeconds - lastProgressSeconds >= 5)
                {
                    lastProgressSeconds = elapsed.Elapsed.TotalSeconds;
                    Log.Debug($"Runtime component download progress. bytes={total}, expectedBytes={expectedLength}, elapsedSeconds={lastProgressSeconds:0.0}, KiBPerSecond={(total - offset) / 1024d / lastProgressSeconds:0.0}");
                }
            }
            if (total == 0)
                throw new InvalidOperationException("Runtime component download is empty.");
            if (expectedLength.HasValue && total != expectedLength.Value ||
                contentLength.HasValue && total - offset != contentLength.Value)
                throw new IOException("Runtime component download ended before the complete file arrived.");
            Log.Debug($"Runtime component transfer completed. bytes={total}, elapsedSeconds={elapsed.Elapsed.TotalSeconds:0.0}");
            return total;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && inactivity.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Runtime component download made no progress for {inactivityTimeout!.Value.TotalSeconds:0.###}s. bytes={GetPartialLength(destinationPath)}", ex);
        }
    }

    static async Task<HttpResponseMessage> SendWithValidatedRedirectsAsync(
        HttpClient http, string url, CancellationToken cancellationToken, long offset = 0)
    {
        Uri current = new(url, UriKind.Absolute);
        for (int redirectCount = 0; ; redirectCount++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("mpv.net", AppInfo.Version.ToString()));
            if (offset > 0)
                request.Headers.Range = new RangeHeaderValue(offset, null);
            HttpResponseMessage response = await http.SendAsync(
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
