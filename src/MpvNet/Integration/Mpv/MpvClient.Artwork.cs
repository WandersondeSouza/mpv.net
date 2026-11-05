using System.Runtime.InteropServices;

using static MpvNet.Native.LibMpv;

namespace MpvNet;

public sealed record AlbumArtworkPixels(int Width, int Height, byte[] Bgra);

public partial class MpvClient
{
    public void AddAlbumArtwork(string path)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return;
        using (operation)
        {
            if (Handle == 0)
                return;
            // video-add/select changes vid. Back it up as a file-local option
            // first, so selecting artwork cannot affect the next media item.
            if (mpv_set_property_string(Handle, "file-local-options/vid", "auto") < 0)
                return;
            using UnmanagedStringArray command = new(["video-add", path, "select", "MPV.NET artwork", "", "yes"]);
            // Decoding a downloaded image must not block the WinForms thread.
            mpv_command_async(Handle, 0, command.Pointer);
        }
    }

    // Copy only the selected attached picture. The existing native-operation
    // gate keeps the result alive until it has been copied and freed.
    public AlbumArtworkPixels? CaptureAlbumArtwork()
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return null;
        using (operation)
        {
            if (Handle == 0 || mpv_get_property(Handle, GetUtf8Bytes("current-tracks/video/albumart"),
                mpv_format.MPV_FORMAT_FLAG, out int albumArt) < 0 || albumArt == 0)
                return null;
            if (mpv_get_property(Handle, GetUtf8Bytes("video-params/w"), mpv_format.MPV_FORMAT_INT64, out long width) < 0 ||
                mpv_get_property(Handle, GetUtf8Bytes("video-params/h"), mpv_format.MPV_FORMAT_INT64, out long height) < 0)
                return null;
            if (width is <= 0 or > 4096 || height is <= 0 or > 4096)
                return null;

            using UnmanagedStringArray command = new(["screenshot-raw", "video", "bgra"]);
            nint resultPointer = Marshal.AllocHGlobal(Marshal.SizeOf<mpv_node>());
            bool initialized = false;
            try
            {
                if (mpv_command_ret(Handle, command.Pointer, resultPointer) < 0)
                    return null;
                initialized = true;
                mpv_node result = Marshal.PtrToStructure<mpv_node>(resultPointer);
                if (result.format != mpv_format.MPV_FORMAT_NODE_MAP || result.list == 0)
                    return null;
                mpv_node_list list = Marshal.PtrToStructure<mpv_node_list>(result.list);
                if (list.num is < 5 or > 16 || list.values == 0 || list.keys == 0)
                    return null;
                Dictionary<string, mpv_node> fields = new(StringComparer.Ordinal);
                for (int i = 0; i < list.num; i++)
                    fields[ConvertFromUtf8(Marshal.ReadIntPtr(list.keys, i * IntPtr.Size))] =
                        Marshal.PtrToStructure<mpv_node>(list.values + i * Marshal.SizeOf<mpv_node>());
                if (!fields.TryGetValue("w", out var w) || w.format != mpv_format.MPV_FORMAT_INT64 ||
                    !fields.TryGetValue("h", out var h) || h.format != mpv_format.MPV_FORMAT_INT64 ||
                    !fields.TryGetValue("stride", out var stride) || stride.format != mpv_format.MPV_FORMAT_INT64 ||
                    !fields.TryGetValue("format", out var format) || format.format != mpv_format.MPV_FORMAT_STRING ||
                    ConvertFromUtf8(format.str) != "bgra" ||
                    !fields.TryGetValue("data", out var data) || data.format != mpv_format.MPV_FORMAT_BYTE_ARRAY || data.ba == 0 ||
                    w.int64 is <= 0 or > 4096 || h.int64 is <= 0 or > 4096)
                    return null;
                var buffer = Marshal.PtrToStructure<ArtworkByteArray>(data.ba);
                long rowBytes = w.int64 * 4;
                // Require a forward, bounded allocation. An unsupported native
                // layout is optional artwork, never a reason to read outside it.
                if (buffer.Data == 0 || stride.int64 < rowBytes || stride.int64 > 4096 * 8 ||
                    buffer.Size < (ulong)((h.int64 - 1) * stride.int64 + rowBytes))
                    return null;
                byte[] pixels = new byte[checked((int)(rowBytes * h.int64))];
                for (int row = 0; row < h.int64; row++)
                    Marshal.Copy(buffer.Data + checked((int)(row * stride.int64)), pixels, row * (int)rowBytes, (int)rowBytes);
                return new((int)w.int64, (int)h.int64, pixels);
            }
            finally
            {
                if (initialized)
                    mpv_free_node_contents(resultPointer);
                Marshal.FreeHGlobal(resultPointer);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ArtworkByteArray
    {
        public nint Data;
        public ulong Size;
    }
}
