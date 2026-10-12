using static MpvNet.Native.LibMpv;

namespace MpvNet;

public partial class MpvClient
{
    internal bool RegisterHook(string name, ulong userData, int priority)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return false;

        using (operation)
        {
            if (Handle == IntPtr.Zero)
                return false;

            try
            {
                mpv_error error = mpv_hook_add(Handle, userData, name, priority);
                if (error >= 0)
                    return true;

                Log.Error($"Could not register mpv hook '{name}'. error={error}");
            }
            catch (EntryPointNotFoundException)
            {
                // Older user-supplied libmpv builds retain the loadfile fallback.
                Log.Error($"The loaded libmpv does not support native hook '{name}'.");
            }

            return false;
        }
    }

    internal void ProcessHook(MpvEventSnapshot snapshot)
    {
        try
        {
            OnHook(snapshot);
        }
        finally
        {
            ContinueHook(snapshot.HookId);
        }
    }

    internal virtual void OnHook(MpvEventSnapshot snapshot) { }

    internal virtual void ContinueHook(ulong id)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return;

        using (operation)
        {
            if (Handle == IntPtr.Zero)
                return;

            mpv_error error = mpv_hook_continue(Handle, id);
            if (error < 0)
                Log.Error($"Could not continue mpv hook. error={error}");
        }
    }
}
