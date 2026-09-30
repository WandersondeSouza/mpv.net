using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MpvNet.Windows.Native;

public static class ApplicationIdentity
{
    // Keep this ID aligned with the installer shortcut and file associations.
    public const string AppUserModelId = "WandersondeSouza.MpvNet";

    public static void Initialize()
    {
        try
        {
            uint length = 0;
            int result = GetCurrentPackageFullName(ref length, nint.Zero);
            // MSIX supplies its own identity. Only customize unpackaged processes.
            if (result != 15700 /* APPMODEL_ERROR_NO_PACKAGE */)
                return;

            using RegistryKey key = Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\AppUserModelId\{AppUserModelId}");
            key.SetValue("DisplayName", AppInfo.Product, RegistryValueKind.String);
            Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(AppUserModelId));
            Log.Debug($"Windows application identity initialized: {AppUserModelId}");
        }
        catch (Exception ex)
        {
            // Shell integration must never prevent playback or application startup.
            Log.Debug($"Windows application identity unavailable: {ex.GetType().Name}, hresult=0x{ex.HResult:X8}");
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, nint packageFullName);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}
