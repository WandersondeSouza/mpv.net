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

    public static Task SaveAsync(string path, string digest, CancellationToken cancellationToken) =>
        SaveAsync(path, new RuntimeComponentMetadata { Digest = digest }, cancellationToken);

    public static async Task SaveAsync(
        string path,
        RuntimeComponentMetadata metadata,
        CancellationToken cancellationToken)
    {
        metadata.LastCheckedUtc = DateTimeOffset.UtcNow;

        string json = JsonSerializer.Serialize(metadata, JsonOptions);
        string directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Metadata path has no directory: {path}");
        Directory.CreateDirectory(directory);
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            await File.WriteAllTextAsync(temporaryPath, json, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            RuntimeComponentFileSystem.DeleteIfExists(temporaryPath);
        }
    }
}
