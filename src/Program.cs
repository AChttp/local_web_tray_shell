using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LocalWebTrayShell
{
    internal static class Program
    {
        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int value);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static int Main(string[] args)
        {
            EnableDpiAwareness();

            if (!EmbeddedDependencyBootstrapper.Initialize(true))
            {
                return 1;
            }

            if (ArgumentHelper.HasFlag(args, "--self-test"))
            {
                return EmbeddedDependencyBootstrapper.RunSelfTest() ? 0 : 1;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new ShellForm());
                return 0;
            }
            catch (Exception ex)
            {
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
