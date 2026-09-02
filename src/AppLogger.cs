using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace LocalWebTrayShell
{
    // Minimal, crash-safe application log. Design constraints:
    //
    //  - NEVER throws. Logging must not become the reason something breaks.
    //  - Each Write opens, appends and closes the file, so every line survives even a
    //    hard hang or process kill (a crashed app cannot flush buffered writers).
    //  - Thread-safe: commands, timers and WebView callbacks log from many threads.
    //  - One file per process session, named switch-yyyyMMdd-HHmmss-pid.log under
    //    %LocalAppData%\SwitchShell\logs, so an intermittent freeze can be tied to the
    //    exact session afterwards. Old sessions are pruned to keep the newest few.
    internal static class AppLogger
    {
        private const int MaxLogFileBytes = 4 * 1024 * 1024;
        private const int RetainedSessions = 12;

        private static readonly object syncRoot = new object();
        private static string sessionLogPath;
        private static bool sessionInitialized;
        private static bool disabled;

        public static string LogDirectory
        {
            get
            {
                return Path.Combine(AppPaths.LocalRootDirectory, "logs");
            }
        }

        // Called once from Main. Writes the session header and prunes old files.
        // Failures disable logging quietly -- the app must still start without it.
        public static void Initialize()
        {
            lock (syncRoot)
            {
                if (sessionInitialized)
                {
                    return;
                }

                sessionInitialized = true;

                try
                {
                    Directory.CreateDirectory(LogDirectory);
                    sessionLogPath = Path.Combine(
                        LogDirectory,
                        "switch-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) +
                        "-pid" + System.Diagnostics.Process.GetCurrentProcess().Id + ".log");

                    WriteRaw("==== Switch 启动 PID=" +
                        System.Diagnostics.Process.GetCurrentProcess().Id +
                        " Version=" + (Program.AppVersion ?? "?") +
                        " OS=" + Environment.OSVersion.VersionString +
                        " 64bit=" + Environment.Is64BitProcess + " ====");

                    PruneOldSessions();
                }
                catch
                {
                    disabled = true;
                }
            }
        }

        public static void Info(string category, string message)
        {
            Write("INFO", category, message);
        }

        public static void Warn(string category, string message)
        {
            Write("WARN", category, message);
        }

        public static void Error(string category, string message)
        {
            Write("ERROR", category, message);
        }

        public static void Error(string category, string message, Exception exception)
        {
            Write("ERROR", category, message + FormatException(exception));
        }

        public static void Flush()
        {
            // Nothing to do: every Write already hit the disk. Kept so callers can
            // express intent on shutdown without a no-op refactor later.
        }

        private static void Write(string level, string category, string message)
        {
            if (!sessionInitialized || disabled)
            {
                return;
            }

            WriteRaw(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                " [" + level + "] [" + (category ?? "?") + "] " +
                (message ?? string.Empty).Replace("\r", "\\r").Replace("\n", "\\n"));
        }

        // Appends one line (newline added) to the current session file. Assumes the
        // caller is outside the lock; takes it here.
        private static void WriteRaw(string line)
        {
            try
            {
                lock (syncRoot)
                {
                    if (sessionLogPath == null)
                    {
                        return;
                    }

                    File.AppendAllText(sessionLogPath, line + Environment.NewLine, Encoding.UTF8);

                    if (new FileInfo(sessionLogPath).Length > MaxLogFileBytes)
                    {
                        // Rotate within the session: stop growing, continue in a -part2 file.
                        sessionLogPath = Path.Combine(
                            Path.GetDirectoryName(sessionLogPath),
                            Path.GetFileNameWithoutExtension(sessionLogPath) + "-part2.log");
                        File.AppendAllText(
                            sessionLogPath,
                            "---- 日志达到 4MB，本会话继续写入新文件 ----" + Environment.NewLine,
                            Encoding.UTF8);
                    }
                }
            }
            catch
            {
                // Disk full, AV lock, permissions -- logging is best effort only.
            }
        }

        private static void PruneOldSessions()
        {
            try
            {
                string[] files = Directory.GetFiles(LogDirectory, "switch-*.log");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase); // timestamp prefix => oldest first

                int excess = files.Length - (RetainedSessions * 2); // x2: allow part2 rotation files
                for (int index = 0; index < excess; index++)
                {
                    try
                    {
                        File.Delete(files[index]);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private static string FormatException(Exception exception)
        {
            if (exception == null)
            {
                return " <no exception>";
            }

            return "\r\n    -> " + exception.GetType().Name + ": " + exception.Message +
                (exception.StackTrace == null
                    ? string.Empty
                    : "\r\n    " + exception.StackTrace.Replace("\n", "\n    ").TrimEnd());
        }
    }
}
