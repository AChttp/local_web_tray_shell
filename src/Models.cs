using System;
using System.Runtime.Serialization;

namespace LocalWebTrayShell
{
    [DataContract]
    internal sealed class AppConfig
    {
        [DataMember(Name = "sites")]
        public SiteEntry[] Sites { get; set; }

        [DataMember(Name = "commands")]
        public CommandEntry[] Commands { get; set; }
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
