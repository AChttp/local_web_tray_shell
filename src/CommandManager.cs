using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace LocalWebTrayShell
{
    internal sealed class CommandManager : IDisposable
    {
        private const int MaxLogLines = 3000;
        private readonly object syncRoot;
        private readonly Dictionary<string, CommandRuntimeState> runtimes;
        private bool disposed;

        public CommandManager()
        {
            syncRoot = new object();
            runtimes = new Dictionary<string, CommandRuntimeState>(StringComparer.OrdinalIgnoreCase);
        }

        public event EventHandler<CommandRuntimeChangedEventArgs> RuntimeChanged;

        public void SyncCommands(IList<CommandEntry> commands)
        {
            Dictionary<string, CommandEntry> incoming =
                new Dictionary<string, CommandEntry>(StringComparer.OrdinalIgnoreCase);
            List<string> removedIds = new List<string>();
            int index;

            if (commands != null)
            {
                for (index = 0; index < commands.Count; index++)
                {
                    if (commands[index] != null && !string.IsNullOrWhiteSpace(commands[index].Id))
                    {
                        incoming[commands[index].Id] = commands[index];
                    }
                }
            }

            lock (syncRoot)
            {
                foreach (string existingId in new List<string>(runtimes.Keys))
                {
                    if (!incoming.ContainsKey(existingId))
                    {
                        DisposeRetryTimerLocked(runtimes[existingId]);
                        runtimes.Remove(existingId);
                        removedIds.Add(existingId);
                    }
                }

                foreach (KeyValuePair<string, CommandEntry> pair in incoming)
                {
                    CommandRuntimeState runtime;

                    if (!runtimes.TryGetValue(pair.Key, out runtime))
                    {
                        runtime = new CommandRuntimeState();
                        runtime.Command = pair.Value;
                        runtime.Status = CommandStatus.Stopped;
                        runtime.Logs = new Queue<CommandLogLine>();
                        runtimes[pair.Key] = runtime;
                        continue;
                    }

                    runtime.Command = pair.Value;
                }
            }

            for (index = 0; index < removedIds.Count; index++)
            {
                RaiseRuntimeChanged(removedIds[index]);
            }
        }

        public CommandRuntimeSnapshot GetSnapshot(string commandId)
        {
            CommandRuntimeState runtime;

            lock (syncRoot)
            {
                if (!runtimes.TryGetValue(commandId, out runtime))
                {
                    return new CommandRuntimeSnapshot
                    {
                        CommandId = commandId,
                        Status = CommandStatus.Stopped
                    };
                }

                return new CommandRuntimeSnapshot
                {
                    CommandId = commandId,
                    Status = runtime.Status,
                    ProcessId = GetProcessId(runtime.Process),
                    ReturnCode = runtime.ReturnCode,
                    RetryAttempts = runtime.RetryAttempts,
                    RetryDueAtUtc = runtime.RetryDueAtUtc,
                    HasProcess = IsProcessActive(runtime.Process)
                };
            }
        }

        public CommandLogSnapshot GetLogSnapshot(string commandId)
        {
            CommandRuntimeState runtime;

            lock (syncRoot)
            {
                if (!runtimes.TryGetValue(commandId, out runtime) || runtime.Logs == null)
                {
                    return new CommandLogSnapshot
                    {
                        CommandId = commandId,
                        Lines = new string[0]
                    };
                }

                return CreateLogSnapshotLocked(commandId, runtime);
            }
        }

        public string[] GetLogs(string commandId)
        {
            return GetLogSnapshot(commandId).Lines;
        }

        public void ClearLogs(string commandId)
        {
            lock (syncRoot)
            {
                if (!runtimes.ContainsKey(commandId))
                {
                    return;
                }

                runtimes[commandId].Logs.Clear();
            }

            RaiseRuntimeChanged(commandId, true);
        }

        public int GetRunningCount()
        {
            int count = 0;

            lock (syncRoot)
            {
                foreach (CommandRuntimeState runtime in runtimes.Values)
                {
                    if (runtime.Status == CommandStatus.Running || runtime.Status == CommandStatus.Starting)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public int GetWaitingRetryCount()
        {
            int count = 0;

            lock (syncRoot)
            {
                foreach (CommandRuntimeState runtime in runtimes.Values)
                {
                    if (runtime.Status == CommandStatus.WaitingRetry)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public bool HasActiveOrPendingCommands()
        {
            lock (syncRoot)
            {
                foreach (CommandRuntimeState runtime in runtimes.Values)
                {
                    if (runtime.Status == CommandStatus.Running ||
                        runtime.Status == CommandStatus.Starting ||
                        runtime.Status == CommandStatus.Stopping ||
                        runtime.Status == CommandStatus.WaitingRetry)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void StartEnabledCommands(IList<CommandEntry> commands)
        {
            int index;

            if (commands == null)
            {
                return;
            }

            for (index = 0; index < commands.Count; index++)
            {
                if (commands[index] != null && commands[index].EnabledOnStart)
                {
                    Start(commands[index].Id);
                }
            }
        }

        public void Start(string commandId)
        {
            StartInternal(commandId, false);
        }

        public void Restart(string commandId)
        {
            bool shouldStartImmediately = false;
            bool shouldKillTree = false;
            int processId = 0;

            lock (syncRoot)
            {
                CommandRuntimeState runtime;

                if (!runtimes.TryGetValue(commandId, out runtime) || runtime.Command == null)
                {
                    return;
                }

                if (runtime.Status == CommandStatus.WaitingRetry)
                {
                    CancelRetryLocked(runtime);
                    runtime.Status = CommandStatus.Stopped;
                    AddLogLocked(runtime, "Pending retry cancelled. Restarting immediately.");
                    shouldStartImmediately = true;
                }
                else if (IsProcessActive(runtime.Process) ||
                         runtime.Status == CommandStatus.Starting ||
                         runtime.Status == CommandStatus.Stopping)
                {
                    runtime.RestartRequested = true;
                    runtime.StopRequested = true;
                    runtime.Status = CommandStatus.Stopping;
                    processId = GetProcessId(runtime.Process).GetValueOrDefault();
                    AddLogLocked(runtime, "Restart requested. Stopping process tree.");
                    shouldKillTree = processId > 0;
                }
                else
                {
                    AddLogLocked(runtime, "Restart requested.");
                    shouldStartImmediately = true;
                }
            }

            RaiseRuntimeChanged(commandId);

            if (shouldKillTree)
            {
                QueueProcessTreeKill(commandId, processId);
            }
            else if (shouldStartImmediately)
            {
                StartInternal(commandId, false);
            }
        }

        public void Stop(string commandId)
        {
            bool shouldKillTree = false;
            int processId = 0;

            lock (syncRoot)
            {
                CommandRuntimeState runtime;

                if (!runtimes.TryGetValue(commandId, out runtime))
                {
                    return;
                }

                runtime.RestartRequested = false;

                if (runtime.Status == CommandStatus.WaitingRetry)
                {
                    CancelRetryLocked(runtime);
                    runtime.Status = CommandStatus.Stopped;
                    AddLogLocked(runtime, "Cancelled pending retry.");
                }
                else if (!IsProcessActive(runtime.Process))
                {
                    CancelRetryLocked(runtime);
                    runtime.Status = CommandStatus.Stopped;
                    runtime.StopRequested = false;
                }
                else
                {
                    CancelRetryLocked(runtime);
                    runtime.StopRequested = true;
                    runtime.Status = CommandStatus.Stopping;
                    processId = GetProcessId(runtime.Process).GetValueOrDefault();
                    AddLogLocked(runtime, "Stopping process tree, PID=" + processId + ".");
                    shouldKillTree = processId > 0;
                }
            }

            RaiseRuntimeChanged(commandId);

            if (shouldKillTree)
            {
                QueueProcessTreeKill(commandId, processId);
            }
        }

        public void StopAll()
        {
            List<string> ids = new List<string>();

            lock (syncRoot)
            {
                foreach (string commandId in runtimes.Keys)
                {
                    ids.Add(commandId);
                }
            }

            for (int index = 0; index < ids.Count; index++)
            {
                Stop(ids[index]);
            }
        }

        public void Dispose()
        {
            List<int> activeProcessIds = new List<int>();

            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;

                foreach (CommandRuntimeState runtime in runtimes.Values)
                {
                    DisposeRetryTimerLocked(runtime);

                    if (IsProcessActive(runtime.Process))
                    {
                        int processId = GetProcessId(runtime.Process).GetValueOrDefault();

                        if (processId > 0)
                        {
                            activeProcessIds.Add(processId);
                        }
                    }
                }
            }

            for (int index = 0; index < activeProcessIds.Count; index++)
            {
                TryKillProcessTreeSilently(activeProcessIds[index]);
            }
        }

        private void StartInternal(string commandId, bool fromRetry)
        {
            CommandEntry command;
            Process process;

            lock (syncRoot)
            {
                CommandRuntimeState runtime;

                if (disposed || !runtimes.TryGetValue(commandId, out runtime) || runtime.Command == null)
                {
                    return;
                }

                if (IsProcessActive(runtime.Process) ||
                    runtime.Status == CommandStatus.Starting ||
                    runtime.Status == CommandStatus.Stopping)
                {
                    return;
                }

                CancelRetryLocked(runtime);
                runtime.RestartRequested = false;
                runtime.StopRequested = false;
                runtime.ReturnCode = null;
                runtime.StartedAtUtc = DateTime.UtcNow;
                runtime.Status = CommandStatus.Starting;

                if (!fromRetry)
                {
                    runtime.RetryAttempts = 0;
                }

                AddLogLocked(runtime, "Starting command: " + runtime.Command.Command);
                command = runtime.Command;
            }

            RaiseRuntimeChanged(commandId);

            try
            {
                process = new Process();
                process.StartInfo = BuildStartInfo(command);
                process.EnableRaisingEvents = true;
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        AppendLog(commandId, e.Data);
                    }
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        AppendLog(commandId, e.Data);
                    }
                };
                process.Exited += delegate
                {
                    HandleProcessExit(commandId, process);
                };

                if (!process.Start())
                {
                    throw new InvalidOperationException("Process did not start.");
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                lock (syncRoot)
                {
                    if (disposed)
                    {
                        TryKillProcessTreeSilently(process.Id);
                        return;
                    }

                    if (!runtimes.ContainsKey(commandId))
                    {
                        TryKillProcessTreeSilently(process.Id);
                        return;
                    }

                    runtimes[commandId].Process = process;
                    runtimes[commandId].Status = CommandStatus.Running;
                    AddLogLocked(runtimes[commandId], "Process started, PID=" + process.Id + ".");
                }

                RaiseRuntimeChanged(commandId);
            }
            catch (Exception ex)
            {
                bool scheduledRetry;

                lock (syncRoot)
                {
                    CommandRuntimeState runtime;

                    if (!runtimes.TryGetValue(commandId, out runtime))
                    {
                        return;
                    }

                    runtime.Process = null;
                    runtime.Status = CommandStatus.Error;
                    AddLogLocked(runtime, "Start failed: " + ex.Message);
                    scheduledRetry = ScheduleRetryLocked(runtime);
                }

                RaiseRuntimeChanged(commandId);

                if (scheduledRetry)
                {
                    RaiseRuntimeChanged(commandId);
                }
            }
        }

        private ProcessStartInfo BuildStartInfo(CommandEntry command)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            string runMode = RunModeCatalog.Normalize(command.RunMode);
            string[] parts;

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;

            if (runMode == RunModeCatalog.Cmd)
            {
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = "/d /c " + WindowsCommandLine.Quote(command.Command);
                return startInfo;
            }

            if (runMode == RunModeCatalog.PowerShell)
            {
                startInfo.FileName = "powershell.exe";
                startInfo.Arguments =
                    "-NoProfile -ExecutionPolicy Bypass -Command " +
                    WindowsCommandLine.Quote(command.Command);
                return startInfo;
            }

            parts = WindowsCommandLine.Split(command.Command);

            if (parts.Length == 0)
            {
                throw new InvalidOperationException("Command is empty.");
            }

            startInfo.FileName = parts[0];
            startInfo.Arguments = WindowsCommandLine.BuildArguments(parts, 1);
            return startInfo;
        }

        private void HandleProcessExit(string commandId, Process process)
        {
            bool restartNow = false;
            int? returnCode = null;

            try
            {
                returnCode = process.ExitCode;
            }
            catch
            {
            }

            lock (syncRoot)
            {
                CommandRuntimeState runtime;

                if (!runtimes.TryGetValue(commandId, out runtime))
                {
                    return;
                }

                if (runtime.Process == null)
                {
                    return;
                }

                if (GetProcessId(runtime.Process) != GetProcessId(process))
                {
                    return;
                }

                runtime.Process = null;
                runtime.ReturnCode = returnCode;
                runtime.RetryDueAtUtc = null;

                if (runtime.RestartRequested)
                {
                    runtime.RestartRequested = false;
                    runtime.StopRequested = false;
                    runtime.Status = CommandStatus.Stopped;
                    runtime.RetryAttempts = 0;
                    AddLogLocked(runtime, "Process stopped. Restarting.");
                    restartNow = true;
                }
                else if (runtime.StopRequested)
                {
                    runtime.StopRequested = false;
                    runtime.Status = CommandStatus.Stopped;
                    runtime.RetryAttempts = 0;
                    AddLogLocked(runtime, "Process stopped, exit code=" + FormatReturnCode(returnCode) + ".");
                }
                else if (returnCode.GetValueOrDefault() == 0)
                {
                    runtime.Status = CommandStatus.Stopped;
                    runtime.RetryAttempts = 0;
                    AddLogLocked(runtime, "Process exited normally, exit code=0.");
                }
                else
                {
                    runtime.Status = CommandStatus.Error;
                    AddLogLocked(runtime, "Process exited unexpectedly, exit code=" + FormatReturnCode(returnCode) + ".");

                    if (runtime.Command != null &&
                        runtime.StartedAtUtc.HasValue &&
                        (DateTime.UtcNow - runtime.StartedAtUtc.Value).TotalSeconds >=
                        runtime.Command.AutoRetry.ResetAfterSeconds)
                    {
                        runtime.RetryAttempts = 0;
                    }

                    ScheduleRetryLocked(runtime);
                }
            }

            RaiseRuntimeChanged(commandId);

            if (restartNow)
            {
                StartInternal(commandId, false);
            }
        }

        private bool ScheduleRetryLocked(CommandRuntimeState runtime)
        {
            AutoRetryConfig retry;
            int delaySeconds;
            int generation;

            if (disposed || runtime.Command == null)
            {
                return false;
            }

            retry = runtime.Command.AutoRetry ?? AppConfigStore.CreateDefaultAutoRetry();

            if (!retry.Enabled)
            {
                return false;
            }

            if (retry.MaxAttempts > 0 && runtime.RetryAttempts >= retry.MaxAttempts)
            {
                AddLogLocked(runtime, "Auto retry stopped after " + retry.MaxAttempts + " attempts.");
                return false;
            }

            runtime.RetryAttempts += 1;
            delaySeconds = GetRetryDelaySeconds(retry, runtime.RetryAttempts);
            runtime.RetryDueAtUtc = DateTime.UtcNow.AddSeconds(delaySeconds);
            runtime.Status = CommandStatus.WaitingRetry;
            generation = ++runtime.RetryGeneration;

            DisposeRetryTimerLocked(runtime);
            runtime.RetryTimer = new System.Threading.Timer(
                OnRetryTimer,
                new RetryContext(runtime.Command.Id, generation),
                delaySeconds * 1000,
                Timeout.Infinite);

            AddLogLocked(
                runtime,
                "Retry scheduled in " + delaySeconds + "s (attempt " + runtime.RetryAttempts + ").");
            return true;
        }

        private void OnRetryTimer(object state)
        {
            RetryContext retryContext = state as RetryContext;
            bool shouldRetry;

            if (retryContext == null)
            {
                return;
            }

            shouldRetry = false;

            lock (syncRoot)
            {
                CommandRuntimeState runtime;

                if (disposed || !runtimes.TryGetValue(retryContext.CommandId, out runtime))
                {
                    return;
                }

                if (runtime.RetryGeneration != retryContext.Generation ||
                    runtime.Status != CommandStatus.WaitingRetry)
                {
                    return;
                }

                runtime.RetryDueAtUtc = null;
                DisposeRetryTimerLocked(runtime);
                AddLogLocked(runtime, "Retrying now.");
                shouldRetry = true;
            }

            RaiseRuntimeChanged(retryContext.CommandId);

            if (shouldRetry)
            {
                StartInternal(retryContext.CommandId, true);
            }
        }

        private void AppendLog(string commandId, string message)
        {
            lock (syncRoot)
            {
                CommandRuntimeState runtime;

                if (!runtimes.TryGetValue(commandId, out runtime))
                {
                    return;
                }

                AddLogLocked(runtime, message);
            }

            RaiseRuntimeChanged(commandId, true);
        }

        private void AddLogLocked(CommandRuntimeState runtime, string message)
        {
            if (runtime.Logs == null)
            {
                runtime.Logs = new Queue<CommandLogLine>();
            }

            while (runtime.Logs.Count >= MaxLogLines)
            {
                runtime.Logs.Dequeue();
            }

            runtime.Logs.Enqueue(new CommandLogLine(
                runtime.NextLogSequence,
                "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message));
            runtime.NextLogSequence += 1;
        }

        private CommandLogSnapshot CreateLogSnapshotLocked(string commandId, CommandRuntimeState runtime)
        {
            string[] lines;
            int index = 0;
            int firstSequence = runtime.NextLogSequence;

            if (runtime.Logs == null || runtime.Logs.Count == 0)
            {
                return new CommandLogSnapshot
                {
                    CommandId = commandId,
                    Lines = new string[0],
                    FirstSequence = firstSequence,
                    NextSequence = runtime.NextLogSequence
                };
            }

            lines = new string[runtime.Logs.Count];

            foreach (CommandLogLine line in runtime.Logs)
            {
                if (index == 0)
                {
                    firstSequence = line.Sequence;
                }

                lines[index] = line.Text;
                index += 1;
            }

            return new CommandLogSnapshot
            {
                CommandId = commandId,
                Lines = lines,
                FirstSequence = firstSequence,
                NextSequence = runtime.NextLogSequence
            };
        }

        private void CancelRetryLocked(CommandRuntimeState runtime)
        {
            runtime.RetryDueAtUtc = null;
            runtime.RetryGeneration += 1;
            DisposeRetryTimerLocked(runtime);
        }

        private void DisposeRetryTimerLocked(CommandRuntimeState runtime)
        {
            if (runtime.RetryTimer == null)
            {
                return;
            }

            runtime.RetryTimer.Dispose();
            runtime.RetryTimer = null;
        }

        private int GetRetryDelaySeconds(AutoRetryConfig retry, int attempt)
        {
            int initialDelay = Math.Max(1, retry.InitialDelaySeconds);
            int maxDelay = Math.Max(initialDelay, retry.MaxDelaySeconds);
            double delay = initialDelay * Math.Pow(2, Math.Max(0, attempt - 1));

            return Math.Min((int)delay, maxDelay);
        }

        private void QueueProcessTreeKill(string commandId, int processId)
        {
            ThreadPool.QueueUserWorkItem(
                delegate
                {
                    try
                    {
                        using (Process killer = new Process())
                        {
                            killer.StartInfo = new ProcessStartInfo
                            {
                                FileName = "taskkill.exe",
                                Arguments = "/PID " + processId + " /T /F",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath)
                            };
                            killer.Start();
                            killer.WaitForExit(5000);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendLog(commandId, "Failed to stop process tree: " + ex.Message);
                    }
                });
        }

        private void TryKillProcessTreeSilently(int processId)
        {
            try
            {
                using (Process killer = new Process())
                {
                    killer.StartInfo = new ProcessStartInfo
                    {
                        FileName = "taskkill.exe",
                        Arguments = "/PID " + processId + " /T /F",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    killer.Start();
                    killer.WaitForExit(5000);
                }
            }
            catch
            {
            }
        }

        private bool IsProcessActive(Process process)
        {
            try
            {
                return process != null && !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private int? GetProcessId(Process process)
        {
            try
            {
                return process != null && !process.HasExited ? (int?)process.Id : null;
            }
            catch
            {
                return null;
            }
        }

        private string FormatReturnCode(int? returnCode)
        {
            return returnCode.HasValue ? returnCode.Value.ToString() : "unknown";
        }

        private void RaiseRuntimeChanged(string commandId)
        {
            RaiseRuntimeChanged(commandId, false);
        }

        private void RaiseRuntimeChanged(string commandId, bool logsOnly)
        {
            EventHandler<CommandRuntimeChangedEventArgs> handler = RuntimeChanged;

            if (handler != null && !disposed)
            {
                handler(this, new CommandRuntimeChangedEventArgs(commandId, logsOnly));
            }
        }

        private sealed class RetryContext
        {
            public RetryContext(string commandId, int generation)
            {
                CommandId = commandId;
                Generation = generation;
            }

            public string CommandId { get; private set; }

            public int Generation { get; private set; }
        }

        private sealed class CommandLogLine
        {
            public CommandLogLine(int sequence, string text)
            {
                Sequence = sequence;
                Text = text;
            }

            public int Sequence { get; private set; }

            public string Text { get; private set; }
        }

        private sealed class CommandRuntimeState
        {
            public CommandEntry Command { get; set; }

            public Process Process { get; set; }

            public Queue<CommandLogLine> Logs { get; set; }

            public int NextLogSequence { get; set; }

            public CommandStatus Status { get; set; }

            public bool StopRequested { get; set; }

            public bool RestartRequested { get; set; }

            public DateTime? StartedAtUtc { get; set; }

            public int? ReturnCode { get; set; }

            public int RetryAttempts { get; set; }

            public DateTime? RetryDueAtUtc { get; set; }

            public System.Threading.Timer RetryTimer { get; set; }

            public int RetryGeneration { get; set; }
        }
    }
}
