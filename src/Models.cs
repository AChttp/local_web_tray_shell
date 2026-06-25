using System;
using System.Runtime.Serialization;
using System.Text;

namespace LocalWebTrayShell
{
    [DataContract]
    internal sealed class AppConfig
    {
        [DataMember(Name = "sites")]
        public SiteEntry[] Sites { get; set; }

        [DataMember(Name = "commands")]
        public CommandEntry[] Commands { get; set; }

        [DataMember(Name = "global_hotkey")]
        public HotkeyConfig GlobalHotkey { get; set; }

        [DataMember(Name = "command_section_ratio")]
        public double CommandSectionRatio { get; set; }
    }

    [DataContract]
    internal sealed class SiteEntry
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "url")]
        public string Url { get; set; }
    }

    [DataContract]
    internal sealed class CommandEntry
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "command")]
        public string Command { get; set; }

        [DataMember(Name = "run_mode")]
        public string RunMode { get; set; }

        [DataMember(Name = "enabled_on_start")]
        public bool EnabledOnStart { get; set; }

        [DataMember(Name = "auto_retry")]
        public AutoRetryConfig AutoRetry { get; set; }

        [DataMember(Name = "working_directory")]
        public string WorkingDirectory { get; set; }

        [DataMember(Name = "environment_variables")]
        public EnvironmentVariableEntry[] EnvironmentVariables { get; set; }
    }

    [DataContract]
    internal sealed class EnvironmentVariableEntry
    {
        [DataMember(Name = "key")]
        public string Key { get; set; }

        [DataMember(Name = "value")]
        public string Value { get; set; }
    }

    [DataContract]
    internal sealed class AutoRetryConfig
    {
        [DataMember(Name = "enabled")]
        public bool Enabled { get; set; }

        [DataMember(Name = "max_attempts")]
        public int MaxAttempts { get; set; }

        [DataMember(Name = "initial_delay_seconds")]
        public int InitialDelaySeconds { get; set; }

        [DataMember(Name = "max_delay_seconds")]
        public int MaxDelaySeconds { get; set; }

        [DataMember(Name = "reset_after_seconds")]
        public int ResetAfterSeconds { get; set; }
    }

    [DataContract]
    internal sealed class HotkeyConfig
    {
        [DataMember(Name = "enabled")]
        public bool Enabled { get; set; }

        [DataMember(Name = "modifiers")]
        public int Modifiers { get; set; }

        [DataMember(Name = "key")]
        public int Key { get; set; }

        public bool HasModifier(int modifier)
        {
            return (Modifiers & modifier) != 0;
        }

        public string ToDisplayString()
        {
            return ToDisplayString(Modifiers, Key);
        }

        public static string ToDisplayString(int modifiers, int key)
        {
            StringBuilder builder = new StringBuilder();

            if ((modifiers & HotkeyConstants.ModControl) != 0)
            {
                builder.Append("Ctrl + ");
            }

            if ((modifiers & HotkeyConstants.ModAlt) != 0)
            {
                builder.Append("Alt + ");
            }

            if ((modifiers & HotkeyConstants.ModShift) != 0)
            {
                builder.Append("Shift + ");
            }

            if ((modifiers & HotkeyConstants.ModWin) != 0)
            {
                builder.Append("Win + ");
            }

            builder.Append(KeyLabel(key));
            return builder.ToString();
        }

        public static string KeyLabel(int key)
        {
            if (key >= 0x30 && key <= 0x39)
            {
                return ((char)key).ToString();
            }

            if (key >= 0x41 && key <= 0x5A)
            {
                return ((char)key).ToString();
            }

            if (key >= 0x70 && key <= 0x7B)
            {
                return "F" + (key - 0x6F);
            }

            switch (key)
            {
                case 0x20: return "Space";
                case 0x09: return "Tab";
                case 0x0D: return "Enter";
                case 0xC0: return "`";
                case 0xBD: return "-";
                case 0xBB: return "=";
                case 0xDB: return "[";
                case 0xDD: return "]";
                case 0xBA: return ";";
                case 0xDE: return "'";
                case 0xBC: return ",";
                case 0xBE: return ".";
                case 0xBF: return "/";
                case 0xDC: return "\\";
                default: return "Key " + key;
            }
        }
    }

    internal static class HotkeyConstants
    {
        public const int ModAlt = 0x0001;
        public const int ModControl = 0x0002;
        public const int ModShift = 0x0004;
        public const int ModWin = 0x0008;
        public const int ModNoRepeat = 0x4000;

        // VK_OEM_3 -- the `~ key. Default combo is Ctrl + `.
        public const int DefaultKey = 0xC0;
        public const int DefaultModifiers = ModControl;

        public static HotkeyConfig CreateDefault()
        {
            return new HotkeyConfig
            {
                Enabled = false,
                Modifiers = DefaultModifiers,
                Key = DefaultKey
            };
        }
    }

    internal enum SiteHealth
    {
        Unknown,
        Up,
        Down
    }

    internal enum CommandStatus
    {
        Stopped,
        Starting,
        Running,
        Stopping,
        WaitingRetry,
        Error
    }

    internal enum WorkspaceMode
    {
        Web,
        Logs
    }

    internal sealed class CommandRuntimeSnapshot
    {
        public string CommandId { get; set; }

        public CommandStatus Status { get; set; }

        public int? ProcessId { get; set; }

        public int? ReturnCode { get; set; }

        public int RetryAttempts { get; set; }

        public DateTime? RetryDueAtUtc { get; set; }

        public bool HasProcess { get; set; }

        public string GetDisplayStatus()
        {
            switch (Status)
            {
                case CommandStatus.Running:
                    return "\u8fd0\u884c\u4e2d";
                case CommandStatus.Starting:
                    return "\u542f\u52a8\u4e2d";
                case CommandStatus.Stopping:
                    return "\u505c\u6b62\u4e2d";
                case CommandStatus.WaitingRetry:
                    return GetRetryRemainingSeconds() + "s \u540e\u91cd\u8bd5";
                case CommandStatus.Error:
                    return "\u9519\u8bef";
                default:
                    return "\u5df2\u505c\u6b62";
            }
        }

        public int GetRetryRemainingSeconds()
        {
            if (!RetryDueAtUtc.HasValue)
            {
                return 0;
            }

            return Math.Max(
                0,
                (int)Math.Ceiling((RetryDueAtUtc.Value - DateTime.UtcNow).TotalSeconds));
        }
    }

    internal sealed class CommandLogSnapshot
    {
        public string CommandId { get; set; }

        public string[] Lines { get; set; }

        public int FirstSequence { get; set; }

        public int NextSequence { get; set; }
    }

    internal sealed class CommandRuntimeChangedEventArgs : EventArgs
    {
        public CommandRuntimeChangedEventArgs(string commandId)
            : this(commandId, false)
        {
        }

        public CommandRuntimeChangedEventArgs(string commandId, bool logsOnly)
        {
            CommandId = commandId;
            LogsOnly = logsOnly;
        }

        public string CommandId { get; private set; }

        public bool LogsOnly { get; private set; }
    }

    internal static class RunModeCatalog
    {
        public const string Direct = "direct";
        public const string Cmd = "cmd";
        public const string PowerShell = "powershell";

        public static string Normalize(string value)
        {
            if (string.Equals(value, Cmd, StringComparison.OrdinalIgnoreCase))
            {
                return Cmd;
            }

            if (string.Equals(value, PowerShell, StringComparison.OrdinalIgnoreCase))
            {
                return PowerShell;
            }

            return Direct;
        }

        public static string GetDisplayName(string value)
        {
            value = Normalize(value);

            if (value == Cmd)
            {
                return "CMD";
            }

            if (value == PowerShell)
            {
                return "PowerShell";
            }

            return "\u76f4\u63a5";
        }
    }
}
