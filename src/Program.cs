using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace LocalWebTrayShell
{
    internal static class Program
    {
        public const string AppVersion = "1.0.5";

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int value);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static int Main(string[] args)
        {
            EnableDpiAwareness();
            AppLogger.Initialize();

            if (!EmbeddedDependencyBootstrapper.Initialize(true))
            {
                AppLogger.Error("startup", "Embedded dependency bootstrapper failed");
                return 1;
            }

            if (ArgumentHelper.HasFlag(args, "--self-test"))
            {
                return EmbeddedDependencyBootstrapper.RunSelfTest() ? 0 : 1;
            }

            // Single-instance: a second launch exits silently. A second Switch.exe would
            // otherwise hit the in-use WebView2 user-data folder and show a confusing error.
            bool createdNew;
            Mutex singleInstanceMutex = new Mutex(
                true,
                @"Local\SwitchShell-SingleInstance",
                out createdNew);

            if (!createdNew)
            {
                AppLogger.Info("startup", "\u5df2\u5728\u8fd0\u884c\uff0c\u7b2c\u4e8c\u4e2a\u5b9e\u4f8b\u9759\u9ed8\u9000\u51fa");
                return 0;
            }

            GC.KeepAlive(singleInstanceMutex);

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
                {
                    AppLogger.Error("ui", "UI \u7ebf\u7a0b\u672a\u5904\u7406\u5f02\u5e38", e.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    AppLogger.Error("runtime", "\u672a\u5904\u7406\u5f02\u5e38 IsTerminating=" + e.IsTerminating, e.ExceptionObject as Exception);
                };
                Application.Run(new ShellForm());
                AppLogger.Info("startup", "\u6d88\u606f\u5faa\u73af\u6b63\u5e38\u9000\u51fa");
                AppLogger.Flush();
                return 0;
            }
            catch (Exception ex)
            {
                AppLogger.Error("startup", "\u542f\u52a8\u5931\u8d25", ex);
                AppLogger.Flush();
                MessageBox.Show(
                    "Switch \u542f\u52a8\u5931\u8d25\u3002\r\n\r\n" + ex,
                    "Switch",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }
        }

        private static void EnableDpiAwareness()
        {
            try
            {
                SetProcessDpiAwareness(2);
            }
            catch
            {
                try
                {
                    SetProcessDPIAware();
                }
                catch
                {
                }
            }
        }
    }

    internal static class ArgumentHelper
    {
        public static bool HasFlag(string[] args, string flag)
        {
            int index;

            if (args == null)
            {
                return false;
            }

            for (index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
