using System;
using System.Runtime.InteropServices;

using MpvNet.Native;

using Xunit;

namespace MpvNet.Tests;

public sealed class LibMpvInteropTests
{
    [Fact]
    public void EventStructuresUseTheNativePointerLayout()
    {
        Assert.Equal(IntPtr.Size == 8 ? 24 : 20, Marshal.SizeOf<LibMpv.mpv_event>());
        Assert.Equal(IntPtr.Size == 8 ? 24 : 12, Marshal.SizeOf<LibMpv.mpv_event_property>());
        Assert.Equal((IntPtr)0, Marshal.OffsetOf<LibMpv.mpv_event_property>(nameof(LibMpv.mpv_event_property.name)));
        Assert.Equal((IntPtr)IntPtr.Size, Marshal.OffsetOf<LibMpv.mpv_event_property>(nameof(LibMpv.mpv_event_property.format)));
        Assert.Equal((IntPtr)(IntPtr.Size * 2), Marshal.OffsetOf<LibMpv.mpv_event_property>(nameof(LibMpv.mpv_event_property.data)));
    }

    [Fact]
    public void EventPropertyNameIsDecodedAsUtf8()
    {
        byte[] name = System.Text.Encoding.UTF8.GetBytes("áudio\0");
        nint pointer = Marshal.AllocHGlobal(name.Length);
        try
        {
            Marshal.Copy(name, 0, pointer, name.Length);
            LibMpv.mpv_event_property property = new() { name = pointer };

            Assert.Equal("áudio", LibMpv.ConvertFromUtf8(property.name));
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    [Fact]
    public void Utf8DecodingPreservesNullTerminationAndInvalidByteReplacement()
    {
        Assert.Empty(LibMpv.ConvertFromUtf8(IntPtr.Zero));

        byte[] bytes = [0xF0, 0x9F, 0x8E, 0xAC, 0xFF, 0, 0x41, 0];
        nint pointer = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            Assert.Equal("\U0001F3AC\uFFFD", LibMpv.ConvertFromUtf8(pointer));
            Assert.Empty(LibMpv.ConvertFromUtf8(pointer + 5));
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    [Fact]
    public void NativePropertyReadersPreserveUtf8AndUnavailableValues()
    {
        using MainPlayer player = NativePlaylistTitleTests.CreateNativePlayer();
        const string property = "user-data/mpvnet-test-title";
        const string value = "Título em português";

        Assert.Empty(player.GetPropertyString(property));
        Assert.Empty(player.GetPropertyOsdString(property));
        Assert.False(player.TryGetPropertyString(property, out string missing));
        Assert.Empty(missing);

        player.SetPropertyString(property, value);

        // libmpv serializes user-data as JSON for MPV_FORMAT_STRING.
        string json = $"\"{value}\"";
        Assert.Equal(json, player.GetPropertyString(property));
        Assert.Equal(value, player.GetPropertyOsdString(property));
        Assert.True(player.TryGetPropertyString(property, out string actual));
        Assert.Equal(json, actual);
    }

    [Fact]
    public void UnmanagedStringArrayWritesUtf8PointersAndNullSentinel()
    {
        using UnmanagedStringArray values = new(new[] { "comando", "áudio" });

        Assert.Equal("comando", LibMpv.ConvertFromUtf8(Marshal.ReadIntPtr(values.Pointer, 0)));
        Assert.Equal("áudio", LibMpv.ConvertFromUtf8(Marshal.ReadIntPtr(values.Pointer, IntPtr.Size)));
        Assert.Equal(IntPtr.Zero, Marshal.ReadIntPtr(values.Pointer, IntPtr.Size * 2));
    }

    [Fact]
    public void UnmanagedStringArrayDisposeIsIdempotent()
    {
        UnmanagedStringArray values = new(new[] { "comando" });

        values.Dispose();
        values.Dispose();

        Assert.Equal(IntPtr.Zero, values.Pointer);
    }
}
