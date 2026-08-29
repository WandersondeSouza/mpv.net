using System;
using System.Runtime.InteropServices;

using MpvNet.Native;

using Xunit;

namespace MpvNet.Tests;

public sealed class MpvEventSnapshotTests
{
    [Fact]
    public void LogPayloadIsCopiedBeforeTheNativeStorageChanges()
    {
        nint prefix = Marshal.StringToCoTaskMemUTF8("prefix");
        nint level = Marshal.StringToCoTaskMemUTF8("info");
        nint text = Marshal.StringToCoTaskMemUTF8("mensagem");
        nint nativeData = Marshal.AllocHGlobal(Marshal.SizeOf<LibMpv.mpv_event_log_message>());
        MpvEventSnapshot snapshot;

        try
        {
            Marshal.StructureToPtr(new LibMpv.mpv_event_log_message
            {
                prefix = prefix,
                level = level,
                text = text,
                log_level = LibMpv.mpv_log_level.MPV_LOG_LEVEL_INFO
            }, nativeData, false);

            snapshot = MpvEventSnapshot.Create(new LibMpv.mpv_event
            {
                event_id = LibMpv.mpv_event_id.MPV_EVENT_LOG_MESSAGE,
                data = nativeData
            });
        }
        finally
        {
            Marshal.FreeHGlobal(nativeData);
            Marshal.FreeCoTaskMem(prefix);
            Marshal.FreeCoTaskMem(level);
            Marshal.FreeCoTaskMem(text);
        }

        Assert.Equal("prefix", snapshot.Prefix);
        Assert.Equal("info", snapshot.LogLevel == LibMpv.mpv_log_level.MPV_LOG_LEVEL_INFO ? "info" : "");
        Assert.Equal("mensagem", snapshot.Text);
    }

    [Fact]
    public void StringPropertyPayloadIsCopiedBeforeTheNativeStorageChanges()
    {
        nint name = Marshal.StringToCoTaskMemUTF8("media-title");
        nint value = Marshal.StringToCoTaskMemUTF8("áudio");
        nint valueCell = Marshal.AllocHGlobal(IntPtr.Size);
        nint nativeData = Marshal.AllocHGlobal(Marshal.SizeOf<LibMpv.mpv_event_property>());
        MpvEventSnapshot snapshot;

        try
        {
            Marshal.WriteIntPtr(valueCell, value);
            Marshal.StructureToPtr(new LibMpv.mpv_event_property
            {
                name = name,
                format = LibMpv.mpv_format.MPV_FORMAT_STRING,
                data = valueCell
            }, nativeData, false);

            snapshot = MpvEventSnapshot.Create(new LibMpv.mpv_event
            {
                event_id = LibMpv.mpv_event_id.MPV_EVENT_PROPERTY_CHANGE,
                data = nativeData
            });
        }
        finally
        {
            Marshal.FreeHGlobal(nativeData);
            Marshal.FreeHGlobal(valueCell);
            Marshal.FreeCoTaskMem(name);
            Marshal.FreeCoTaskMem(value);
        }

        Assert.Equal("media-title", snapshot.PropertyName);
        Assert.Equal(LibMpv.mpv_format.MPV_FORMAT_STRING, snapshot.PropertyFormat);
        Assert.Equal("áudio", snapshot.PropertyValue);
    }
}
