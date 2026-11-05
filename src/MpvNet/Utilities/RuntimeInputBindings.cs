using System.Text.Json;

namespace MpvNet;

internal static class RuntimeInputBindings
{
    // input-bindings reflects explicit --input-conf files and script sections,
    // unlike the frontend's startup copy of input.conf.
    public static List<Binding> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        using JsonDocument document = JsonDocument.Parse(json);
        Dictionary<string, JsonElement> active = new(StringComparer.Ordinal);
        foreach (JsonElement binding in document.RootElement.EnumerateArray())
        {
            long priority = binding.GetProperty("priority").GetInt64();
            if (priority < 0)
                continue;

            string key = binding.GetProperty("key").GetString()!;
            bool weak = binding.GetProperty("is_weak").GetBoolean();
            // Match mpv's stats.lua: strong user/forced bindings precede weak
            // defaults, then the highest priority wins. Keep the first on ties.
            if (!active.TryGetValue(key, out JsonElement previous) ||
                (previous.GetProperty("is_weak").GetBoolean() && !weak) ||
                (previous.GetProperty("is_weak").GetBoolean() == weak &&
                 priority > previous.GetProperty("priority").GetInt64()))
                active[key] = binding;
        }

        return active.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => new Binding
        {
            Input = entry.Key,
            Command = entry.Value.GetProperty("cmd").GetString()!,
            Comment = entry.Value.TryGetProperty("comment", out JsonElement comment) ? comment.GetString() ?? "" : ""
        }).ToList();
    }
}
