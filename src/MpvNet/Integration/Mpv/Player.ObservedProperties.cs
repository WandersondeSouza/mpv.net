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
            Log.Debug($"Observed playlist-pos change. value={value}, fileEnded={FileEnded}, appExit={App.Exit}, keepOpen='{GetPropertyString("keep-open")}'");

            if (FileEnded && value == -1)
                if (GetPropertyString("keep-open") == "no" && App.Exit)
                {
                    Log.Info("Requesting mpv quit because playback ended, playlist is empty, keep-open=no, and App.Exit is true.");
                    CommandV("quit");
                }
        });

        ObserveProperty("playlist", ScheduleAutocreatedPlaylistNormalization);
    }
}
