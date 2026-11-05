using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MpvNet;

// Owns metadata request policy; the caller owns HttpClient and binary downloads.
internal sealed class GitHubReleaseMetadataClient(HttpClient http, TimeSpan requestTimeout, TimeProvider? timeProvider = null)
{
    internal const int MaximumResponseBytes = 4 * 1024 * 1024;
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    readonly SemaphoreSlim _gate = new(1);
    DateTimeOffset _retryAfter;

    public async Task<GitHubRelease> GetReleaseAsync(string url, CancellationToken cancellationToken)
    {
        GitHubReleaseClient.ValidateGitHubUri(url);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(requestTimeout);
        try
        {
            await _gate.WaitAsync(timeout.Token).ConfigureAwait(false);
            try
            {
                timeout.Token.ThrowIfCancellationRequested();
                if (_clock.GetUtcNow() < _retryAfter)
                    throw new HttpRequestException(
                        $"GitHub release metadata requests are paused until {_retryAfter:O}.", null, HttpStatusCode.TooManyRequests);

                using HttpResponseMessage response = await GitHubReleaseClient.SendWithValidatedRedirectsAsync(
                    http, url, timeout.Token).ConfigureAwait(false);
                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests ||
                    HasNoRemainingRequests(response))
                {
                    _retryAfter = GetRetryAfter(response, _clock.GetUtcNow());
                    Log.Debug($"GitHub release metadata rate limit. status={(int)response.StatusCode}, retryAfter={_retryAfter:O}");
                }
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaximumResponseBytes)
                    throw new InvalidDataException("GitHub release metadata exceeds the response limit.");

                using Stream input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                using var output = new MemoryStream();
                byte[] buffer = new byte[8192];
                int read;
                while ((read = await input.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) != 0)
                {
                    if (output.Length + read > MaximumResponseBytes)
                        throw new InvalidDataException("GitHub release metadata exceeds the response limit.");
                    output.Write(buffer, 0, read);
                }
                GitHubRelease? release = JsonSerializer.Deserialize<GitHubRelease>(
                    output.GetBuffer().AsSpan(0, (int)output.Length), JsonOptions);
                if (release?.Assets is null || release.Assets.Any(asset => asset is null))
                    throw new InvalidDataException("Invalid GitHub release payload.");
                return release;
            }
            finally { _gate.Release(); }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            Log.Error($"Timed out while reading GitHub release metadata after {requestTimeout.TotalSeconds:0.###}s. url='{Log.SafeValue(url)}'");
            throw;
        }
    }

    static bool HasNoRemainingRequests(HttpResponseMessage response) =>
        response.Headers.TryGetValues("x-ratelimit-remaining", out var remaining) && remaining.Contains("0");

    static DateTimeOffset GetRetryAfter(HttpResponseMessage response, DateTimeOffset now)
    {
        DateTimeOffset? retryAt = response.Headers.RetryAfter?.Date;
        if (response.Headers.RetryAfter?.Delta is { } delay)
            retryAt = delay <= DateTimeOffset.MaxValue - now ? now + delay : DateTimeOffset.MaxValue;
        if (HasNoRemainingRequests(response) && response.Headers.TryGetValues("x-ratelimit-reset", out var reset) &&
            long.TryParse(reset.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out long seconds) &&
            seconds is >= 0 and <= 253402300799)
        {
            DateTimeOffset resetAt = DateTimeOffset.FromUnixTimeSeconds(seconds);
            if (retryAt is null || resetAt > retryAt)
                retryAt = resetAt;
        }
        // Fail fast during the cooldown; do not keep the bootstrap waiting or retry immediately.
        return retryAt > now ? retryAt.Value : now.AddMinutes(1);
    }
}
