
using System.Runtime.InteropServices;
using System.Threading;

using static MpvNet.Native.LibMpv;

namespace MpvNet;

public class MpvClient
{
    public event Action<string[]>? ClientMessage;            // client-message      MPV_EVENT_CLIENT_MESSAGE
    public event Action<mpv_log_level, string>? LogMessage;  // log-message         MPV_EVENT_LOG_MESSAGE
    public event Action<mpv_end_file_reason>? EndFile;       // end-file            MPV_EVENT_END_FILE
    public event Action? Shutdown;                           // shutdown            MPV_EVENT_SHUTDOWN
    public event Action? GetPropertyReply;                   // get-property-reply  MPV_EVENT_GET_PROPERTY_REPLY
    public event Action? SetPropertyReply;                   // set-property-reply  MPV_EVENT_SET_PROPERTY_REPLY
    public event Action? CommandReply;                       // command-reply       MPV_EVENT_COMMAND_REPLY
    public event Action? StartFile;                          // start-file          MPV_EVENT_START_FILE
    public event Action? FileLoaded;                         // file-loaded         MPV_EVENT_FILE_LOADED
    public event Action? VideoReconfig;                      // video-reconfig      MPV_EVENT_VIDEO_RECONFIG
    public event Action? AudioReconfig;                      // audio-reconfig      MPV_EVENT_AUDIO_RECONFIG
    public event Action? Seek;                               // seek                MPV_EVENT_SEEK
    public event Action? PlaybackRestart;                    // playback-restart    MPV_EVENT_PLAYBACK_RESTART

    public Dictionary<string, List<Action>> PropChangeActions { get; set; } = [];
    public Dictionary<string, List<Action<int>>> IntPropChangeActions { get; set; } = [];
    public Dictionary<string, List<Action<bool>>> BoolPropChangeActions { get; set; } = [];
    public Dictionary<string, List<Action<double>>> DoublePropChangeActions { get; set; } = [];
    public Dictionary<string, List<Action<string>>> StringPropChangeActions { get; set; } = [];

    public nint Handle { get; set; }

    readonly object _nativeLifetimeLock = new();
    readonly ReaderWriterLockSlim _nativeLifetimeGate = new(LockRecursionPolicy.NoRecursion);
    bool _acceptingNativeOperations = true;

    internal void BeginShutdown()
    {
        lock (_nativeLifetimeLock)
            _acceptingNativeOperations = false;
    }

    internal bool TryEnterNativeOperation(out IDisposable? operation)
    {
        lock (_nativeLifetimeLock)
        {
            if (!_acceptingNativeOperations)
            {
                operation = null;
                return false;
            }

            _nativeLifetimeGate.EnterReadLock();
        }

        operation = new NativeOperationLease(_nativeLifetimeGate);
        return true;
    }

    sealed class NativeOperationLease(ReaderWriterLockSlim gate) : IDisposable
    {
        public void Dispose() => gate.ExitReadLock();
    }

    internal void DestroyHandle()
    {
        BeginShutdown();
        _nativeLifetimeGate.EnterWriteLock();
        try
        {
            nint handle = Handle;
            if (handle == IntPtr.Zero)
                return;

            mpv_destroy(handle);
            Handle = IntPtr.Zero;
        }
        finally
        {
            _nativeLifetimeGate.ExitWriteLock();
        }
    }

    public void EventLoop() => EventLoop(CancellationToken.None);

    public void EventLoop(CancellationToken cancellationToken)
    {
        nint handle = Handle;

        while (!cancellationToken.IsCancellationRequested && TryEnterNativeOperation(out IDisposable? operation))
        {
            mpv_event evt = default;
            using (operation)
            {
                handle = Handle;
                if (handle == IntPtr.Zero)
                    return;

                IntPtr ptr = mpv_wait_event(handle, 0.1);
                if (ptr == IntPtr.Zero)
                    continue;

                evt = (mpv_event)Marshal.PtrToStructure(ptr, typeof(mpv_event))!;

            }

            try
                {
                    switch (evt.event_id)
                    {
                        case mpv_event_id.MPV_EVENT_SHUTDOWN:
                            OnShutdown();
                            return;
                        case mpv_event_id.MPV_EVENT_LOG_MESSAGE:
                            {
                                var data = (mpv_event_log_message)Marshal.PtrToStructure(evt.data, typeof(mpv_event_log_message))!;
                                OnLogMessage(data);
                            }
                            break;
                        case mpv_event_id.MPV_EVENT_CLIENT_MESSAGE:
                            {
                                var data = (mpv_event_client_message)Marshal.PtrToStructure(evt.data, typeof(mpv_event_client_message))!;
                                OnClientMessage(data);
                            }
                            break;
                        case mpv_event_id.MPV_EVENT_VIDEO_RECONFIG:
                            OnVideoReconfig();
                            break;
                        case mpv_event_id.MPV_EVENT_END_FILE:
                            {
                                var data = (mpv_event_end_file)Marshal.PtrToStructure(evt.data, typeof(mpv_event_end_file))!;
                                OnEndFile(data);
                            }
                            break;
                        case mpv_event_id.MPV_EVENT_FILE_LOADED:  // triggered after MPV_EVENT_START_FILE
                            OnFileLoaded();
                            break;
                        case mpv_event_id.MPV_EVENT_PROPERTY_CHANGE:
                            {
                                var data = (mpv_event_property)Marshal.PtrToStructure(evt.data, typeof(mpv_event_property))!;
                                OnPropertyChange(data);
                            }
                            break;
                        case mpv_event_id.MPV_EVENT_GET_PROPERTY_REPLY:
                            OnGetPropertyReply();
                            break;
                        case mpv_event_id.MPV_EVENT_SET_PROPERTY_REPLY:
                            OnSetPropertyReply();
                            break;
                        case mpv_event_id.MPV_EVENT_COMMAND_REPLY:
                            OnCommandReply();
                            break;
                        case mpv_event_id.MPV_EVENT_START_FILE:  // triggered before MPV_EVENT_FILE_LOADED
                            OnStartFile();
                            break;
                        case mpv_event_id.MPV_EVENT_AUDIO_RECONFIG:
                            OnAudioReconfig();
                            break;
                        case mpv_event_id.MPV_EVENT_SEEK:
                            OnSeek();
                            break;
                        case mpv_event_id.MPV_EVENT_PLAYBACK_RESTART:
                            OnPlaybackRestart();
                            break;
                    }
                }
            catch (Exception ex)
            {
                Terminal.WriteError(ex);
            }
        }
    }

    protected virtual void OnClientMessage(mpv_event_client_message data) =>
        ClientMessage?.Invoke(ConvertFromUtf8Strings(data.args, data.num_args));

    protected virtual void OnLogMessage(mpv_event_log_message data)
    {
        if (LogMessage != null)
        {
            string msg = $"[{ConvertFromUtf8(data.prefix)}] {ConvertFromUtf8(data.text)}";
            LogMessage.Invoke(data.log_level, msg);
        }
    }

    protected virtual void OnPropertyChange(mpv_event_property data)
    {
        string name = ConvertFromUtf8(data.name);

        if (data.format == mpv_format.MPV_FORMAT_FLAG)
        {
            bool value = Marshal.PtrToStructure<int>(data.data) != 0;

            foreach (var action in GetActions(BoolPropChangeActions, name))
                action.Invoke(value);
        }
        else if (data.format == mpv_format.MPV_FORMAT_STRING)
        {
            string value = ConvertFromUtf8(Marshal.PtrToStructure<IntPtr>(data.data));

            foreach (var action in GetActions(StringPropChangeActions, name))
                action.Invoke(value);
        }
        else if (data.format == mpv_format.MPV_FORMAT_INT64)
        {
            int value = Convert.ToInt32(Marshal.PtrToStructure<long>(data.data));

            foreach (var action in GetActions(IntPropChangeActions, name))
                action.Invoke(value);
        }
        else if (data.format == mpv_format.MPV_FORMAT_NONE)
        {
            foreach (var action in GetActions(PropChangeActions, name))
                action.Invoke();
        }
        else if (data.format == mpv_format.MPV_FORMAT_DOUBLE)
        {
            double value = Marshal.PtrToStructure<double>(data.data);

            foreach (var action in GetActions(DoublePropChangeActions, name))
                action.Invoke(value);
        }
    }

    static Action<T>[] GetActions<T>(Dictionary<string, List<Action<T>>> actions, string name)
    {
        lock (actions)
        {
            if (!actions.TryGetValue(name, out var values))
                return [];

            return [.. values];
        }
    }

    static Action[] GetActions(Dictionary<string, List<Action>> actions, string name)
    {
        lock (actions)
        {
            if (!actions.TryGetValue(name, out var values))
                return [];

            return [.. values];
        }
    }

    protected virtual void OnEndFile(mpv_event_end_file data) => EndFile?.Invoke((mpv_end_file_reason)data.reason);
    protected virtual void OnFileLoaded() => FileLoaded?.Invoke();
    protected virtual void OnShutdown() => Shutdown?.Invoke();
    protected virtual void OnGetPropertyReply() => GetPropertyReply?.Invoke();
    protected virtual void OnSetPropertyReply() => SetPropertyReply?.Invoke();
    protected virtual void OnCommandReply() => CommandReply?.Invoke();
    protected virtual void OnStartFile() => StartFile?.Invoke();
    protected virtual void OnVideoReconfig() => VideoReconfig?.Invoke();
    protected virtual void OnAudioReconfig() => AudioReconfig?.Invoke();
    protected virtual void OnSeek() => Seek?.Invoke();
    protected virtual void OnPlaybackRestart() => PlaybackRestart?.Invoke();

    public void Command(string command)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return;

        using (operation)
        {
            nint handle = Handle;
            if (handle == IntPtr.Zero)
                return;

            mpv_error err = mpv_command_string(handle, command);

            if (err < 0)
                HandleError(err, "error executing command: " + command);
        }
    }

    public void CommandV(params string[] args)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return;

        using (operation)
        {
        nint handle = Handle;
        if (handle == IntPtr.Zero)
            return;

        int count = args.Length + 1;
        IntPtr[] pointers = new IntPtr[count];
        IntPtr rootPtr = Marshal.AllocHGlobal(IntPtr.Size * count);

        for (int index = 0; index < args.Length; index++)
        {
            var bytes = GetUtf8Bytes(args[index]);
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            pointers[index] = ptr;
        }

        Marshal.Copy(pointers, 0, rootPtr, count);
        mpv_error err = mpv_command(handle, rootPtr);

        foreach (IntPtr ptr in pointers)
            Marshal.FreeHGlobal(ptr);

        Marshal.FreeHGlobal(rootPtr);

        if (err < 0)
            HandleError(err, "error executing command: " + string.Join("\n", args));
        }
    }

    public string Expand(string? value)
    {
        if (value == null)
            return "";

        if (!value.Contains("${"))
            return value;

        if (!TryEnterNativeOperation(out IDisposable? operation))
            return "property expansion error";

        using (operation)
        {
        nint handle = Handle;
        if (handle == IntPtr.Zero)
            return "property expansion error";

        string[] args = { "expand-text", value };
        int count = args.Length + 1;
        IntPtr[] pointers = new IntPtr[count];
        IntPtr rootPtr = Marshal.AllocHGlobal(IntPtr.Size * count);

        for (int index = 0; index < args.Length; index++)
        {
            var bytes = GetUtf8Bytes(args[index]);
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            pointers[index] = ptr;
        }

        Marshal.Copy(pointers, 0, rootPtr, count);
        IntPtr resultNodePtr = Marshal.AllocHGlobal(16);
        mpv_error err = mpv_command_ret(handle, rootPtr, resultNodePtr);

        foreach (IntPtr ptr in pointers)
        {
            Marshal.FreeHGlobal(ptr);
        }

        Marshal.FreeHGlobal(rootPtr);

        if (err < 0)
        {
            HandleError(err, "error executing command: " + string.Join("\n", args));
            Marshal.FreeHGlobal(resultNodePtr);
            return "property expansion error";
        }

        mpv_node resultNode = Marshal.PtrToStructure<mpv_node>(resultNodePtr);
        string ret = ConvertFromUtf8(resultNode.str);
        mpv_free_node_contents(resultNodePtr);
        Marshal.FreeHGlobal(resultNodePtr);
        return ret;
        }
    }

    public bool GetPropertyBool(string name, bool handleError = true)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return false;

        using (operation)
        {
        nint handle = Handle;
        if (handle == IntPtr.Zero)
            return false;

        mpv_error err = mpv_get_property(handle, GetUtf8Bytes(name),
            mpv_format.MPV_FORMAT_FLAG, out int value);

        if (err < 0 && handleError)
            HandleError(err, "error getting property: " + name);

        return err >= 0 && value != 0;
        }
    }

    public void SetPropertyBool(string name, bool value)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return;

        using (operation)
        {
        nint handle = Handle;
        if (handle == IntPtr.Zero)
            return;

        int val = value ? 1 : 0;
        mpv_error err = mpv_set_property(handle, GetUtf8Bytes(name), mpv_format.MPV_FORMAT_FLAG, ref val);

        if (err < 0)
            HandleError(err, $"error setting property: {name} = {value}");
        }
    }

    public int GetPropertyInt(string name)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return 0;

        using (operation)
        {
        nint handle = Handle;
        if (handle == IntPtr.Zero)
            return 0;

        mpv_error err = mpv_get_property(handle, GetUtf8Bytes(name),
            mpv_format.MPV_FORMAT_INT64, out long value);

        if (err < 0 && App.DebugMode)
            HandleError(err, "error getting property: " + name);

        return err >= 0 ? Convert.ToInt32(value) : 0;
        }
    }

    public void SetPropertyInt(string name, int value)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return;

        using (operation)
        {
        nint handle = Handle;
        if (handle == IntPtr.Zero)
            return;

        long val = value;
        mpv_error err = mpv_set_property(handle, GetUtf8Bytes(name), mpv_format.MPV_FORMAT_INT64, ref val);

        if (err < 0)
            HandleError(err, $"error setting property: {name} = {value}");
        }
    }

    public void SetPropertyLong(string name, long value)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return;

        using (operation)
        {
        nint handle = Handle;
        if (handle == IntPtr.Zero)
            return;

        mpv_error err = mpv_set_property(handle, GetUtf8Bytes(name), mpv_format.MPV_FORMAT_INT64, ref value);

        if (err < 0)
            HandleError(err, $"error setting property: {name} = {value}");
        }
    }

    public long GetPropertyLong(string name)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return 0;

        using (operation)
        {
        nint handle = Handle;
        if (handle == IntPtr.Zero)
            return 0;

        mpv_error err = mpv_get_property(handle, GetUtf8Bytes(name),
            mpv_format.MPV_FORMAT_INT64, out long value);

        if (err < 0)
            HandleError(err, "error getting property: " + name);

        return err >= 0 ? value : 0;
        }
    }

    public double GetPropertyDouble(string name, bool handleError = true)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return 0;

        using (operation)
        {
        nint handle = Handle;
        if (handle == IntPtr.Zero)
            return 0;

        mpv_error err = mpv_get_property(handle, GetUtf8Bytes(name),
            mpv_format.MPV_FORMAT_DOUBLE, out double value);

        if (err < 0 && handleError && App.DebugMode)
            HandleError(err, "error getting property: " + name);

        return value;
        }
    }

    public void SetPropertyDouble(string name, double value)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return;

        using (operation)
        {
        nint handle = Handle;
        if (handle == IntPtr.Zero)
            return;

        double val = value;
        mpv_error err = mpv_set_property(handle, GetUtf8Bytes(name), mpv_format.MPV_FORMAT_DOUBLE, ref val);

        if (err < 0)
            HandleError(err, $"error setting property: {name} = {value}");
        }
    }

    public string GetPropertyString(string name)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return "";

        using (operation)
        {
        nint handle = Handle;
        if (handle == IntPtr.Zero)
            return "";

        mpv_error err = mpv_get_property(handle, GetUtf8Bytes(name),
            mpv_format.MPV_FORMAT_STRING, out IntPtr lpBuffer);

        if (err == 0)
        {
            string ret = ConvertFromUtf8(lpBuffer);
            mpv_free(lpBuffer);
            return ret;
        }

        if (err < 0 && App.DebugMode)
            HandleError(err, "error getting property: " + name);

        return "";
        }
    }

    public void SetPropertyString(string name, string value)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return;

        using (operation)
        {
            nint handle = Handle;
            if (handle == IntPtr.Zero)
                return;

            mpv_error err = mpv_set_property_string(handle, name, value);

            if (err < 0)
                HandleError(err, $"error setting property: {name} = {value}");
        }
    }

    public void SetOptionString(string name, string value)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return;

        using (operation)
        {
            nint handle = Handle;
            if (handle == IntPtr.Zero)
                return;

            mpv_error err = (mpv_error)mpv_set_option_string(handle, GetUtf8Bytes(name), GetUtf8Bytes(value));

            if (err < 0)
                HandleError(err, $"error setting option: {name} = {value}");
        }
    }

    public string GetPropertyOsdString(string name)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return "";

        using (operation)
        {
        nint handle = Handle;
        if (handle == IntPtr.Zero)
            return "";

        mpv_error err = mpv_get_property(handle, GetUtf8Bytes(name),
            mpv_format.MPV_FORMAT_OSD_STRING, out IntPtr lpBuffer);

        if (err == 0)
        {
            string ret = ConvertFromUtf8(lpBuffer);
            mpv_free(lpBuffer);
            return ret;
        }

        if (err < 0)
            HandleError(err, "error getting property: " + name);

        return "";
        }
    }

    public void ObservePropertyInt(string name, Action<int> action)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return;

        using (operation)
        {
        lock (IntPropChangeActions)
        {
            if (!IntPropChangeActions.ContainsKey(name))
            {
                mpv_error err = mpv_observe_property(Handle, 0, name, mpv_format.MPV_FORMAT_INT64);

                if (err < 0)
                    HandleError(err, "error observing property: " + name);
                else
                    IntPropChangeActions[name] = [];
            }

            if (IntPropChangeActions.ContainsKey(name))
                IntPropChangeActions[name].Add(action);
        }
        }
    }

    public void ObservePropertyDouble(string name, Action<double> action)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return;

        using (operation)
        {
        lock (DoublePropChangeActions)
        {
            if (!DoublePropChangeActions.ContainsKey(name))
            {
                mpv_error err = mpv_observe_property(Handle, 0, name, mpv_format.MPV_FORMAT_DOUBLE);

                if (err < 0)
                    HandleError(err, "error observing property: " + name);
                else
                    DoublePropChangeActions[name] = [];
            }

            if (DoublePropChangeActions.ContainsKey(name))
                DoublePropChangeActions[name].Add(action);
        }
        }
    }

    public void ObservePropertyBool(string name, Action<bool> action)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return;

        using (operation)
        {
        lock (BoolPropChangeActions)
        {
            if (!BoolPropChangeActions.ContainsKey(name))
            {
                mpv_error err = mpv_observe_property(Handle, 0, name, mpv_format.MPV_FORMAT_FLAG);

                if (err < 0)
                    HandleError(err, "error observing property: " + name);
                else
                    BoolPropChangeActions[name] = [];
            }

            if (BoolPropChangeActions.ContainsKey(name))
                BoolPropChangeActions[name].Add(action);
        }
        }
    }

    public void ObservePropertyString(string name, Action<string> action)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return;

        using (operation)
        {
        lock (StringPropChangeActions)
        {
            if (!StringPropChangeActions.ContainsKey(name))
            {
                mpv_error err = mpv_observe_property(Handle, 0, name, mpv_format.MPV_FORMAT_STRING);

                if (err < 0)
                    HandleError(err, "error observing property: " + name);
                else
                    StringPropChangeActions[name] = [];
            }

            if (StringPropChangeActions.ContainsKey(name))
                StringPropChangeActions[name].Add(action);
        }
        }
    }

    public void ObserveProperty(string name, Action action)
    {
        if (!TryEnterNativeOperation(out IDisposable? operation))
            return;

        using (operation)
        {
        lock (PropChangeActions)
        {
            if (!PropChangeActions.ContainsKey(name))
            {
                mpv_error err = mpv_observe_property(Handle, 0, name, mpv_format.MPV_FORMAT_NONE);

                if (err < 0)
                    HandleError(err, "error observing property: " + name);
                else
                    PropChangeActions[name] = [];
            }

            if (PropChangeActions.ContainsKey(name))
                PropChangeActions[name].Add(action);
        }
        }
    }

    static void HandleError(mpv_error err, string msg)
    {
        Terminal.WriteError(msg);
        Terminal.WriteError(GetError(err));
    }
}
