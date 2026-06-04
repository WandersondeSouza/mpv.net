namespace MpvNet;

public partial class MainPlayer
{
    void ConfigureObservedProperties()
    {
        ObservePropertyBool("pause", value => {
            Paused = value;
            Pause?.Invoke();
        });

        VideoRotate = GetPropertyInt("video-rotate");

        ObservePropertyInt("video-rotate", value =>
        {
            if (VideoRotate != value)
            {
                VideoRotate = value;
                UpdateVideoSize("dwidth", "dheight");
            }
        });

        ObservePropertyInt("playlist-pos", value => {
            PlaylistPos = value;
            PlaylistPosChanged?.Invoke(value);

            if (FileEnded && value == -1)
                if (GetPropertyString("keep-open") == "no" && App.Exit)
                    CommandV("quit");
        });

        ObserveProperty("playlist", ScheduleAutocreatedPlaylistNormalization);
    }
}
