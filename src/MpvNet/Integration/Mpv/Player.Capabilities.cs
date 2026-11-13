using System.Threading;
using System.Threading.Tasks;

using System.Text;
using System.Text.Json;

using MpvNet.Help;

using static MpvNet.Native.LibMpv;

namespace MpvNet;

public partial class MainPlayer
{
    public string[] ProfileNames
    {
        get
        {
            if (_profileNames != null)
                return _profileNames;

            string[] ignore = ["builtin-pseudo-gui", "encoding", "libmpv", "pseudo-gui", "default"];
            string json = GetPropertyString("profile-list");
            using JsonDocument document = JsonDocument.Parse(json);
            return _profileNames = document.RootElement.EnumerateArray()
                .Select(it => it.GetProperty("name").GetString())
                .OfType<string>()
                .Where(it => !ignore.Contains(it))
                .ToArray();
        }
    }

    public string GetProfiles()
    {
        string json = GetPropertyString("profile-list");
        StringBuilder sb = new StringBuilder();
        using JsonDocument document = JsonDocument.Parse(json);

        foreach (JsonElement profile in document.RootElement.EnumerateArray())
        {
            sb.Append(profile.GetProperty("name").GetString() + BR2);

            foreach (var it in profile.GetProperty("options").EnumerateArray())
                sb.AppendLine($"    {it.GetProperty("key").GetString()} = {it.GetProperty("value").GetString()}");

            sb.Append(BR);
        }

        return sb.ToString();
    }

    public string GetDecoders()
    {
        using JsonDocument document = JsonDocument.Parse(GetPropertyString("decoder-list"));
        string[] list = document.RootElement.EnumerateArray()
            .Select(it => $"{it.GetProperty("codec").GetString()} - {it.GetProperty("description").GetString()}")
            .OrderBy(it => it)
            .ToArray();

        return string.Join(BR, list);
    }

    public string GetProtocols() => string.Join(BR, GetPropertyString("protocol-list").Split(',').OrderBy(i => i));

    public string GetDemuxers() => string.Join(BR, GetPropertyString("demuxer-lavf-list").Split(',').OrderBy(i => i));

    public MpvClient CreateNewPlayer(string name)
    {
        lock (_destroyLock)
        {
            ObjectDisposedException.ThrowIf(_isDestroyed, this);
            nint mainHandle = MainHandle;
            if (mainHandle == IntPtr.Zero)
                throw new InvalidOperationException("The main libmpv context is not available.");

            var client = new MpvClient();
            try
            {
                client.Handle = mpv_create_client(mainHandle, name);
                if (client.Handle == IntPtr.Zero)
                    throw new InvalidOperationException($"libmpv could not create client '{name}'.");

                Clients.Add(client);
                CancellationToken cancellationToken = PlayerCancellationToken;
                TrackEventTask(Task.Run(() => client.EventLoop(cancellationToken), cancellationToken));
                return client;
            }
            catch
            {
                Clients.Remove(client);
                client.Dispose();
                throw;
            }
        }
    }
}
