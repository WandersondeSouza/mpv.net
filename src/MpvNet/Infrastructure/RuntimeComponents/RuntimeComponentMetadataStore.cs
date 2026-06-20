using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MpvNet;

internal static class RuntimeComponentMetadataStore
{
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<RuntimeComponentMetadata?> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return null;

        string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<RuntimeComponentMetadata>(json, JsonOptions);
    }

    public static async Task SaveAsync(string path, string digest, CancellationToken cancellationToken)
    {
        var metadata = new RuntimeComponentMetadata
        {
            Digest = digest,
            LastCheckedUtc = DateTimeOffset.UtcNow
        };

        string json = JsonSerializer.Serialize(metadata, JsonOptions);
        await File.WriteAllTextAsync(path, json, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }
}
