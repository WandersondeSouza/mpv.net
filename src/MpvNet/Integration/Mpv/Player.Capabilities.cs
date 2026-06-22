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
            return _profileNames = JsonDocument.Parse(json).RootElement.EnumerateArray()
                .Select(it => it.GetProperty("name").GetString())
                .Where(it => !ignore.Contains(it)).ToArray()!;
        }
    }

    public string GetProfiles()
    {
        string json = GetPropertyString("profile-list");
        StringBuilder sb = new StringBuilder();

        foreach (var profile in JsonDocument.Parse(json).RootElement.EnumerateArray())
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
        var list = JsonDocument.Parse(GetPropertyString("decoder-list")).RootElement.EnumerateArray()
            .Select(it => $"{it.GetProperty("codec").GetString()} - {it.GetProperty("description").GetString()}")
            .OrderBy(it => it);

        return string.Join(BR, list);
    }

    public string GetProtocols() => string.Join(BR, GetPropertyString("protocol-list").Split(',').OrderBy(i => i));

    public string GetDemuxers() => string.Join(BR, GetPropertyString("demuxer-lavf-list").Split(',').OrderBy(i => i));

    public MpvClient CreateNewPlayer(string name)
    {
        var client = new MpvClient { Handle = mpv_create_client(MainHandle, name) };

        if (client.Handle == IntPtr.Zero)
            throw new Exception("Error CreateNewPlayer");

        BackgroundTaskRunner.Run(client.EventLoop);
        Clients.Add(client);
        return client;
    }
}
