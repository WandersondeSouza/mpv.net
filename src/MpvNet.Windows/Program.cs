
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;

using MpvNet.Windows.Native;
using MpvNet.Help;
using MpvNet.Windows.UI;
using MpvNet.Windows.Help;
using MpvNet.Windows.WPF;

namespace MpvNet.Windows;

static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            Log.Info("Application starting.");
            RegistryHelp.ProductName = AppInfo.Product;
            Translator.Current = new WpfTranslator();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    Log.Error(ex, "Unhandled application domain exception.");
                else
                    Log.Error("Unhandled application domain exception: " + e.ExceptionObject);

                Terminal.WriteError(e.ExceptionObject);
            };
            Application.ThreadException += (sender, e) =>
            {
                Log.Error(e.Exception, "Unhandled Windows Forms thread exception.");
                Terminal.WriteError(e.Exception);
            };
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Log.Error(e.Exception, "Unobserved task exception.");
                Terminal.WriteError(e.Exception);
            };

            if (App.IsTerminalAttached)
                WinApi.AttachConsole(-1 /*ATTACH_PARENT_PROCESS*/);

            string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            Log.Debug($"Command line arguments received: count={args.Length}, args={Log.SafeValues(args)}");

            if (args.Length > 1 && args[0] == "--register-file-associations")
            {
                Log.Info($"Registering file associations from command line. perceivedType='{Log.SafeValue(args[1])}', extensions={Log.SafeValues(args.Skip(2))}");
                FileAssociation.Register(args[1], args.Skip(2).ToArray());
                return;
            }

            App.Init();
            Theme.Init();
            Log.Info("Application initialized.");
            using Mutex mutex = new Mutex(true, StringHelp.GetMD5Hash(App.ConfPath), out bool isFirst);

            if (Control.ModifierKeys == Keys.Shift ||
                App.CommandLine.Contains("--process-instance=multi") ||
                App.CommandLine.Contains("--o="))
            {
                App.ProcessInstance = "multi";
                Log.Debug($"Process instance forced to multi. shift={Control.ModifierKeys == Keys.Shift}, containsMulti={App.CommandLine.Contains("--process-instance=multi")}, containsOutputOption={App.CommandLine.Contains("--o=")}");
            }

            if ((App.ProcessInstance == "single" || App.ProcessInstance == "queue") && !isFirst)
            {
                Log.Info($"Forwarding command line to existing mpv.net instance. mode={App.ProcessInstance}");
                List<string> args2 = new List<string> { App.ProcessInstance };

                foreach (string arg in args)
                {
                    if (CommandLine.IsLoadableFileArgument(arg))
                    {
                        Log.Debug($"Forwarding loadable argument to existing instance: '{Log.SafeValue(arg)}'");
                        args2.Add(arg);
                    }
                    else if (arg == "--queue")
                    {
                        Log.Debug("Forwarding request switches existing instance mode to queue.");
                        args2[0] = "queue";
                    }
                    else if (arg.StartsWith("--command="))
                    {
                        args2[0] = "command";
                        args2.Add(arg[10..]);
                        Log.Debug($"Forwarding command to existing instance: '{Log.SafeValue(arg[10..])}'");
                    }
                }

                Process[] procs = Process.GetProcessesByName("mpvnet");

                for (int i = 0; i < 20; i++)
                {
                    foreach (Process proc in procs)
                    {
                        if (proc.MainWindowHandle != IntPtr.Zero)
                        {
                            WinApi.AllowSetForegroundWindow(proc.Id);
                            var data = new WinApi.CopyDataStruct();
                            data.lpData = string.Join("\n", args2.ToArray());
                            data.cbData = data.lpData.Length * 2 + 1;
                            WinApi.SendMessage(proc.MainWindowHandle, 0x004A /*WM_COPYDATA*/, IntPtr.Zero, ref data);

                            if (App.IsTerminalAttached)
                                WinApi.FreeConsole();

                            Log.Info("Command line forwarded to existing instance.");
                            return;
                        }
                    }

                    Thread.Sleep(50);
                }

                return;
            }

            if (ProcessCommandLineArguments())
            {
                Log.Info("Processed informational command line argument.");
                Environment.GetCommandLineArgs();
            }
            else if (App.CommandLine.Contains("--o="))
            {
                Log.Info("Starting headless output mode because --o= was supplied.");
                App.AutoLoadFolder = false;
                Player.Init(IntPtr.Zero, true);
                CommandLine.ProcessCommandLineArgsPostInit();
                CommandLine.ProcessCommandLineFiles();
                Log.Debug("Headless output mode sets idle=no before entering mpv event loop.");
                Player.SetPropertyString("idle", "no");
                Player.EventLoop();
                Player.Destroy();
            }
            else
            {
                WpfApplication.Init();
                Application.Run(new WinForms.MainForm());
            }

            if (App.IsTerminalAttached)
                WinApi.FreeConsole();

            Log.Info("Application shutting down.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Application startup failed.");
            Terminal.WriteError(ex);
        }
    }

    static bool ProcessCommandLineArguments()
    {
        foreach (string arg in Environment.GetCommandLineArgs().Skip(1))
        {
            Log.Debug($"Checking informational command line argument: '{Log.SafeValue(arg)}'");

            if (arg == "--profile=help")
            {
                Log.Info("Processing --profile=help.");
                Player.Init(IntPtr.Zero, false);
                Console.WriteLine(Player.GetProfiles());
                Player.Destroy();
                return true;
            }
            else if (arg == "--vd=help" || arg == "--ad=help")
            {
                Log.Info($"Processing decoder help argument: '{arg}'.");
                Player.Init(IntPtr.Zero, false);
                Console.WriteLine(Player.GetDecoders());
                Player.Destroy();
                return true;
            }
            else if (arg == "--audio-device=help")
            {
                Log.Info("Processing --audio-device=help.");
                Player.Init(IntPtr.Zero, false);
                Console.WriteLine(Player.GetPropertyOsdString("audio-device-list"));
                Player.Destroy();
                return true;
            }
            else if (arg == "--input-keylist")
            {
                Log.Info("Processing --input-keylist.");
                Player.Init(IntPtr.Zero, false);
                Console.WriteLine(Player.GetPropertyString("input-key-list").Replace(",", BR));
                Player.Destroy();
                return true;
            }
            else if (arg == "--version")
            {
                Log.Info("Processing --version.");
                Player.Init(IntPtr.Zero, false);
                Console.WriteLine(AppClass.About);
                Player.Destroy();
                return true;
            }
        }

        return false;
    }
}
