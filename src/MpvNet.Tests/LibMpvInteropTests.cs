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
