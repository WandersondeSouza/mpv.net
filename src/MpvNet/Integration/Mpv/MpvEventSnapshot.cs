using System.Runtime.InteropServices;

using static MpvNet.Native.LibMpv;

namespace MpvNet;

internal sealed class MpvEventSnapshot
{
    public MpvEventSnapshot(mpv_event_id eventId) => EventId = eventId;

    public mpv_event_id EventId { get; }
    public mpv_log_level LogLevel { get; private set; }
    public string Prefix { get; private set; } = "";
    public string Text { get; private set; } = "";
    public string[] ClientMessage { get; private set; } = [];
    public int EndFileReason { get; private set; }
    public int EndFileError { get; private set; }
    public string PropertyName { get; private set; } = "";
    public mpv_format PropertyFormat { get; private set; }
    public object? PropertyValue { get; private set; }

    public static MpvEventSnapshot Create(mpv_event nativeEvent)
    {
        MpvEventSnapshot snapshot = new(nativeEvent.event_id);

        switch (nativeEvent.event_id)
        {
            case mpv_event_id.MPV_EVENT_LOG_MESSAGE:
                mpv_event_log_message log = Marshal.PtrToStructure<mpv_event_log_message>(nativeEvent.data);
                snapshot.LogLevel = log.log_level;
                snapshot.Prefix = ConvertFromUtf8(log.prefix);
                snapshot.Text = ConvertFromUtf8(log.text);
                break;
            case mpv_event_id.MPV_EVENT_CLIENT_MESSAGE:
                mpv_event_client_message message = Marshal.PtrToStructure<mpv_event_client_message>(nativeEvent.data);
                snapshot.ClientMessage = ConvertFromUtf8Strings(message.args, message.num_args);
                break;
            case mpv_event_id.MPV_EVENT_END_FILE:
                mpv_event_end_file endFile = Marshal.PtrToStructure<mpv_event_end_file>(nativeEvent.data);
                snapshot.EndFileReason = endFile.reason;
                snapshot.EndFileError = endFile.error;
                break;
            case mpv_event_id.MPV_EVENT_PROPERTY_CHANGE:
                mpv_event_property property = Marshal.PtrToStructure<mpv_event_property>(nativeEvent.data);
                snapshot.PropertyName = ConvertFromUtf8(property.name);
                snapshot.PropertyFormat = property.format;
                snapshot.PropertyValue = ReadPropertyValue(property);
                break;
        }

        return snapshot;
    }

    static object? ReadPropertyValue(mpv_event_property property) =>
        property.format switch
        {
            mpv_format.MPV_FORMAT_FLAG => property.data != IntPtr.Zero && Marshal.ReadInt32(property.data) != 0,
            mpv_format.MPV_FORMAT_STRING => property.data == IntPtr.Zero ? "" : ConvertFromUtf8(Marshal.ReadIntPtr(property.data)),
            mpv_format.MPV_FORMAT_INT64 => property.data == IntPtr.Zero ? 0L : Marshal.ReadInt64(property.data),
            mpv_format.MPV_FORMAT_DOUBLE => property.data == IntPtr.Zero ? 0d : BitConverter.Int64BitsToDouble(Marshal.ReadInt64(property.data)),
            _ => null
        };
}
