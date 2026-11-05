using System.Text.Json;

namespace MpvNet.Windows.Services.MediaTransport;

internal static class MusicBrainzReleaseSearch
{
    public static string? SelectRelease(byte[] json, AudioArtworkIdentity identity)
    {
        SearchResponse? response = JsonSerializer.Deserialize<SearchResponse>(json);
        List<(string Id, string Group, bool HasGroup)> matches = [];
        foreach (Release? release in response?.Releases ?? [])
        {
            if (release is null || release.Score is null or < 95 || release.Title is null ||
                AudioArtworkIdentity.Normalize(release.Title) != AudioArtworkIdentity.Normalize(identity.Album!) ||
                !Guid.TryParse(release.Id, out Guid releaseId) || release.ArtistCredit is null)
                continue;
            string credited = string.Concat(release.ArtistCredit.Select(credit =>
                (credit?.Name ?? "") + (credit?.JoinPhrase ?? "")));
            if (AudioArtworkIdentity.Normalize(credited) != AudioArtworkIdentity.Normalize(identity.Artist!))
                continue;
            bool hasGroup = Guid.TryParse(release.ReleaseGroup?.Id, out Guid groupId);
            matches.Add((releaseId.ToString("D"), hasGroup ? groupId.ToString("D") : releaseId.ToString("D"), hasGroup));
        }
        // Multiple editions are acceptable only when they identify the same album.
        if (matches.Count == 0 || matches.Select(match => match.Group).Distinct().Count() != 1)
            return null;
        // CAA chooses the group's representative cover instead of an arbitrary pressing.
        return matches[0].HasGroup ? "release-group/" + matches[0].Group : "release/" + matches[0].Id;
    }

    sealed class SearchResponse
    {
        [JsonPropertyName("releases")]
        public Release?[]? Releases { get; set; }
    }

    sealed class Release
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("score")]
        public int? Score { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("artist-credit")]
        public ArtistCredit?[]? ArtistCredit { get; set; }

        [JsonPropertyName("release-group")]
        public ReleaseGroup? ReleaseGroup { get; set; }
    }

    sealed class ArtistCredit
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("joinphrase")]
        public string? JoinPhrase { get; set; }
    }

    sealed class ReleaseGroup
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
