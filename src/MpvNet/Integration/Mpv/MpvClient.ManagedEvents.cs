using static MpvNet.Native.LibMpv;

namespace MpvNet;

public partial class MpvClient
{
    internal virtual void OnClientMessage(MpvEventSnapshot data) =>
        ClientMessage?.Invoke(data.ClientMessage);

    internal virtual void OnLogMessage(MpvEventSnapshot data)
    {
        if (LogMessage != null)
        {
            string msg = $"[{data.Prefix}] {data.Text}";
            LogMessage.Invoke(data.LogLevel, msg);
        }
    }

    internal virtual void OnPropertyChange(MpvEventSnapshot data)
    {
        if (data.PropertyFormat == mpv_format.MPV_FORMAT_FLAG)
        {
            bool value = data.PropertyValue is bool flag && flag;

            foreach (var action in GetActions(BoolPropChangeActions, data.PropertyName))
                action.Invoke(value);
        }
        else if (data.PropertyFormat == mpv_format.MPV_FORMAT_STRING)
        {
            string value = data.PropertyValue as string ?? "";

            foreach (var action in GetActions(StringPropChangeActions, data.PropertyName))
                action.Invoke(value);
        }
        else if (data.PropertyFormat == mpv_format.MPV_FORMAT_INT64)
        {
            int value = Convert.ToInt32(data.PropertyValue);

            foreach (var action in GetActions(IntPropChangeActions, data.PropertyName))
                action.Invoke(value);
        }
        else if (data.PropertyFormat == mpv_format.MPV_FORMAT_NONE)
        {
            foreach (var action in GetActions(PropChangeActions, data.PropertyName))
                action.Invoke();
        }
        else if (data.PropertyFormat == mpv_format.MPV_FORMAT_DOUBLE)
        {
            double value = data.PropertyValue is double number ? number : 0d;

            foreach (var action in GetActions(DoublePropChangeActions, data.PropertyName))
                action.Invoke(value);
        }
    }

    internal virtual void OnEndFile(MpvEventSnapshot data) =>
        EndFile?.Invoke((mpv_end_file_reason)data.EndFileReason);
}
