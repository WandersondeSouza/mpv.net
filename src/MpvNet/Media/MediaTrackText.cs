using MpvNet.Extensions;

namespace MpvNet;

internal static class MediaTrackText
{
    public static void AddMpvValue(MediaTrack track, object? value)
    {
        string text = (value + "").Trim();

        if (text != "" && !track.Text.Contains(text))
            track.Text += " " + text + ",";
    }

    public static void AddMediaInfoValue(MediaTrack track, object? value)
    {
        string text = value?.ToStringEx().Trim() ?? "";

        if (text != "" && !(track.Text != null && track.Text.Contains(text)))
            track.Text += " " + text + ",";
    }
}
