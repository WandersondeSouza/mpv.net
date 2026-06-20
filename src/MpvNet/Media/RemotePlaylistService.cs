using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MpvNet;

internal static class RemotePlaylistService
{
    public static bool TryDownload(
        string mediaInput,
        HttpClient httpClient,
        string tempFolder,
        ICollection<string> tempFiles,
        out string playlistFile)
    {
        playlistFile = "";

        if (!ShouldProbe(mediaInput))
            return false;

        try
        {
            using HttpRequestMessage probeRequest = new(HttpMethod.Get, mediaInput);
            using HttpResponseMessage probeResponse = httpClient.Send(
                probeRequest, HttpCompletionOption.ResponseHeadersRead);

            if (!probeResponse.IsSuccessStatusCode)
                return false;

            using Stream probeStream = probeResponse.Content.ReadAsStream();
            byte[] buffer = new byte[4096];
            int read = probeStream.Read(buffer, 0, buffer.Length);

            if (!LooksLikeM3u(buffer.AsSpan(0, read)))
                return false;

            string content = httpClient.GetStringAsync(mediaInput).GetAwaiter().GetResult();
            Directory.CreateDirectory(tempFolder);
            string tempFile = Path.Combine(tempFolder, Guid.NewGuid() + ".m3u8");
            File.WriteAllText(tempFile, content, Encoding.UTF8);
            tempFiles.Add(tempFile);
            playlistFile = tempFile;
            return true;
        }
        catch (Exception ex)
        {
            LogDetectionFailure(mediaInput, ex);
            return false;
        }
    }

    public static bool IsProbeTimeout(Exception exception) =>
        exception is TaskCanceledException or TimeoutException ||
        exception.InnerException is not null && IsProbeTimeout(exception.InnerException);

    public static bool LooksLikeM3u(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return false;

        ReadOnlySpan<byte> utf8Bom = [0xEF, 0xBB, 0xBF];
        if (bytes.StartsWith(utf8Bom))
            bytes = bytes[utf8Bom.Length..];

        while (!bytes.IsEmpty && bytes[0] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            bytes = bytes[1..];

        return bytes.StartsWith("#EXTM3U"u8);
    }

    static bool ShouldProbe(string mediaInput)
    {
        if (!Uri.TryCreate(mediaInput, UriKind.Absolute, out Uri? uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        return !FileTypes.IsPlaylistFile(mediaInput) && !FileTypes.IsVideoFile(mediaInput);
    }

    static void LogDetectionFailure(string mediaInput, Exception exception)
    {
        if (IsProbeTimeout(exception))
        {
            Log.Debug($"Remote playlist detection timed out for '{mediaInput}': {exception.Message}");
            return;
        }

        Log.Error(exception,
            $"Remote playlist detection failed without blocking playback. path='{Log.SafeValue(mediaInput)}'");
    }
}
