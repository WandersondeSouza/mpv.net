
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

            if (args.Length > 1 && args[0] == "--register-file-associations")
            {
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
            }

            if ((App.ProcessInstance == "single" || App.ProcessInstance == "queue") && !isFirst)
            {
                List<string> args2 = new List<string> { App.ProcessInstance };

                foreach (string arg in args)
                {
                    if (CommandLine.IsLoadableFileArgument(arg))
                        args2.Add(arg);
                    else if (arg == "--queue")
                        args2[0] = "queue";
                    else if (arg.StartsWith("--command="))
                    {
                        args2[0] = "command";
                        args2.Add(arg[10..]);
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

                            return;
                        }
                    }

                    Thread.Sleep(50);
                }

                return;
            }

            if (ProcessCommandLineArguments())
                Environment.GetCommandLineArgs();
            else if (App.CommandLine.Contains("--o="))
            {
                App.AutoLoadFolder = false;
                Player.Init(IntPtr.Zero, true);
                CommandLine.ProcessCommandLineArgsPostInit();
                CommandLine.ProcessCommandLineFiles();
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
            if (arg == "--profile=help")
            {
                Player.Init(IntPtr.Zero, false);
                Console.WriteLine(Player.GetProfiles());
                Player.Destroy();
                return true;
            }
            else if (arg == "--vd=help" || arg == "--ad=help")
            {
                Player.Init(IntPtr.Zero, false);
                Console.WriteLine(Player.GetDecoders());
                Player.Destroy();
                return true;
            }
            else if (arg == "--audio-device=help")
            {
                Player.Init(IntPtr.Zero, false);
                Console.WriteLine(Player.GetPropertyOsdString("audio-device-list"));
                Player.Destroy();
                return true;
            }
            else if (arg == "--input-keylist")
            {
                Player.Init(IntPtr.Zero, false);
                Console.WriteLine(Player.GetPropertyString("input-key-list").Replace(",", BR));
                Player.Destroy();
                return true;
            }
            else if (arg == "--version")
            {
                Player.Init(IntPtr.Zero, false);
                Console.WriteLine(AppClass.About);
                Player.Destroy();
                return true;
            }
        }

        return false;
    }
}
