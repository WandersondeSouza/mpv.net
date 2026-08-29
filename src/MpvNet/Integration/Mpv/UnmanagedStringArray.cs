using System.Runtime.InteropServices;

using static MpvNet.Native.LibMpv;

namespace MpvNet;

internal sealed class UnmanagedStringArray : IDisposable
{
    readonly nint[] _strings;
    nint _root;

    public UnmanagedStringArray(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        _strings = new nint[values.Count];

        try
        {
            _root = Marshal.AllocHGlobal(checked(IntPtr.Size * (values.Count + 1)));

            for (int index = 0; index < values.Count; index++)
            {
                byte[] bytes = GetUtf8Bytes(values[index]);
                nint pointer = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, pointer, bytes.Length);
                _strings[index] = pointer;
            }

            if (_strings.Length > 0)
                Marshal.Copy(_strings, 0, _root, _strings.Length);

            Marshal.WriteIntPtr(_root, IntPtr.Size * _strings.Length, IntPtr.Zero);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public nint Pointer => _root;

    public void Dispose()
    {
        for (int index = 0; index < _strings.Length; index++)
        {
            nint pointer = _strings[index];
            if (pointer == IntPtr.Zero)
                continue;

            Marshal.FreeHGlobal(pointer);
            _strings[index] = IntPtr.Zero;
        }

        if (_root == IntPtr.Zero)
            return;

        Marshal.FreeHGlobal(_root);
        _root = IntPtr.Zero;
    }
}
