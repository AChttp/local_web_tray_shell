using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;

namespace LocalWebTrayShell
{
    internal static class AppConfigStore
    {
        public static AppConfig Load()
        {
            AppConfig config;

            if (!File.Exists(AppPaths.ConfigPath))
            {
                config = CreateDefaultConfig();

                if (File.Exists(AppPaths.LegacySitesPath))
                {
                    config.Sites = LoadLegacySites();
                }

                Save(config);
                return config;
            }

            try
            {
                using (FileStream stream = new FileStream(AppPaths.ConfigPath, FileMode.Open, FileAccess.Read))
                {
                    DataContractJsonSerializer serializer =
                        new DataContractJsonSerializer(typeof(AppConfig));
                    config = serializer.ReadObject(stream) as AppConfig;
                }
            }
            catch
            {
                // The config on disk is unreadable. Keep running with defaults, but
                // preserve the bad file as a backup so the user can recover it --
                // otherwise the next Save() would silently overwrite it with defaults.
                BackupCorruptConfig();
                config = CreateDefaultConfig();
            }

            config = Sanitize(config);

            if (config.Sites.Length == 0)
            {
                config.Sites = CreateDefaultSites();
            }

            if (config.Commands == null)
            {
                config.Commands = new CommandEntry[0];
            }

            return config;
        }

        public static void Save(AppConfig config)
        {
            config = Sanitize(config);
            Directory.CreateDirectory(AppPaths.LocalRootDirectory);

            // Write to a temp file then atomically replace, so a crash/AV lock mid-write
            // cannot leave a truncated config that would trip Load()'s corrupt-config path.
            string tempPath = AppPaths.ConfigPath + ".tmp";

            using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            {
                DataContractJsonSerializer serializer =
                    new DataContractJsonSerializer(typeof(AppConfig));
                serializer.WriteObject(stream, config);
            }

            try
            {
                if (File.Exists(AppPaths.ConfigPath))
                {
                    string backupPath = AppPaths.ConfigPath + ".bak";
                    File.Replace(tempPath, AppPaths.ConfigPath, backupPath);
                }
                else
                {
                    File.Move(tempPath, AppPaths.ConfigPath);
                }
            }
            catch
            {
                // If the atomic move failed, fall back to leaving the existing file intact
                // rather than risking a partial write. Best-effort cleanup of the temp file.
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                }
            }
        }

        private static void BackupCorruptConfig()
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(AppPaths.ConfigPath);
                string backupPath = AppPaths.ConfigPath + ".corrupt-" + DateTime.UtcNow.Ticks;

                using (FileStream stream = new FileStream(backupPath, FileMode.Create, FileAccess.Write))
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
            }
            catch
            {
            }
        }

        // Export a sanitized config to an arbitrary path (import/export feature).
        public static void SaveTo(string path, AppConfig config)
        {
            config = Sanitize(config);
            string directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                DataContractJsonSerializer serializer =
                    new DataContractJsonSerializer(typeof(AppConfig));
                serializer.WriteObject(stream, config);
            }
        }

        // Import a config from an arbitrary path; returns null on failure.
        public static AppConfig LoadFrom(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    DataContractJsonSerializer serializer =
                        new DataContractJsonSerializer(typeof(AppConfig));
                    AppConfig config = serializer.ReadObject(stream) as AppConfig;

                    return config == null ? null : Sanitize(config);
                }
            }
            catch
            {
                return null;
            }
        }

        private static SiteEntry[] LoadLegacySites()
        {
            try
            {
                using (FileStream stream = new FileStream(AppPaths.LegacySitesPath, FileMode.Open, FileAccess.Read))
                {
                    DataContractJsonSerializer serializer =
                        new DataContractJsonSerializer(typeof(List<SiteEntry>));
                    List<SiteEntry> loaded = serializer.ReadObject(stream) as List<SiteEntry>;
                    return SanitizeSites(loaded);
                }
            }
            catch
            {
                return CreateDefaultSites();
            }
        }

        private static AppConfig CreateDefaultConfig()
        {
            return new AppConfig
            {
                Sites = CreateDefaultSites(),
                Commands = new CommandEntry[0]
            };
        }

        private static SiteEntry[] CreateDefaultSites()
        {
            return new[]
            {
                new SiteEntry
                {
                    Id = NewId("site"),
                    Name = "Main 8080",
                    Url = "http://127.0.0.1:8080/#/"
                },
                new SiteEntry
                {
                    Id = NewId("site"),
                    Name = "Panel 8099",
                    Url = "http://127.0.0.1:8099/"
                }
            };
        }

        private static AppConfig Sanitize(AppConfig config)
        {
            if (config == null)
            {
                config = CreateDefaultConfig();
            }

            return new AppConfig
            {
                Sites = SanitizeSites(config.Sites),
                Commands = SanitizeCommands(config.Commands),
                GlobalHotkey = SanitizeHotkey(config.GlobalHotkey),
                CommandSectionRatio = SanitizeRatio(config.CommandSectionRatio)
            };
        }

        public const double DefaultCommandSectionRatio = 0.42;

        private static double SanitizeRatio(double ratio)
        {
            if (ratio < 0.20 || ratio > 0.80 || double.IsNaN(ratio) || double.IsInfinity(ratio))
            {
                return DefaultCommandSectionRatio;
            }

            return ratio;
        }

        private static EnvironmentVariableEntry[] SanitizeEnvironmentVariables(EnvironmentVariableEntry[] variables)
        {
            if (variables == null)
            {
                return new EnvironmentVariableEntry[0];
            }

            Dictionary<string, string> unique =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<EnvironmentVariableEntry> results = new List<EnvironmentVariableEntry>();

            for (int index = 0; index < variables.Length; index++)
            {
                EnvironmentVariableEntry entry = variables[index];

                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                unique[entry.Key.Trim()] = entry.Value ?? string.Empty;
            }

            foreach (KeyValuePair<string, string> pair in unique)
            {
                results.Add(new EnvironmentVariableEntry
                {
                    Key = pair.Key,
                    Value = pair.Value
                });
            }

            return results.ToArray();
        }

        private static HotkeyConfig SanitizeHotkey(HotkeyConfig config)
        {
            if (config == null)
            {
                return HotkeyConstants.CreateDefault();
            }

            return new HotkeyConfig
            {
                Enabled = config.Enabled,
                Modifiers = config.Modifiers & (HotkeyConstants.ModAlt | HotkeyConstants.ModControl | HotkeyConstants.ModShift | HotkeyConstants.ModWin),
                Key = config.Key
            };
        }

        // Normalizes a site URL the same way SanitizeSites does, so dedup logic is
        // identical everywhere (the dialog checks for duplicates before add/edit must
        // match the persistence-layer dedup, or two URLs that normalize to the same
        // AbsoluteUri would be accepted by the dialog but silently collapsed on save).
        public static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            Uri uri;

            if (Uri.TryCreate(url.Trim(), UriKind.Absolute, out uri) &&
                (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                return uri.AbsoluteUri;
            }

            return url.Trim();
        }

        private static SiteEntry[] SanitizeSites(IList<SiteEntry> sites)
        {
            Dictionary<string, SiteEntry> uniqueSites =
                new Dictionary<string, SiteEntry>(StringComparer.OrdinalIgnoreCase);
            List<SiteEntry> results = new List<SiteEntry>();
            int index;

            if (sites == null)
            {
                return results.ToArray();
            }

            for (index = 0; index < sites.Count; index++)
            {
                SiteEntry site = sites[index];
                string normalizedUrl;
                string name;
                string id;

                if (site == null || string.IsNullOrWhiteSpace(site.Url))
                {
                    continue;
                }

                normalizedUrl = NormalizeUrl(site.Url);

                if (string.IsNullOrEmpty(normalizedUrl) ||
                    !normalizedUrl.StartsWith(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (uniqueSites.ContainsKey(normalizedUrl))
                {
                    continue;
                }

                Uri uri;
                Uri.TryCreate(normalizedUrl, UriKind.Absolute, out uri);

                name = string.IsNullOrWhiteSpace(site.Name)
                    ? (uri == null ? normalizedUrl : uri.Host + (uri.IsDefaultPort ? string.Empty : ":" + uri.Port))
                    : site.Name.Trim();
                id = string.IsNullOrWhiteSpace(site.Id) ? NewId("site") : site.Id.Trim();

                site = new SiteEntry
                {
                    Id = id,
                    Name = name,
                    Url = normalizedUrl
                };

                uniqueSites[normalizedUrl] = site;
                results.Add(site);
            }

            return results.ToArray();
        }

        private static CommandEntry[] SanitizeCommands(IList<CommandEntry> commands)
        {
            HashSet<string> usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<CommandEntry> results = new List<CommandEntry>();
            int index;

            if (commands == null)
            {
                return results.ToArray();
            }

            for (index = 0; index < commands.Count; index++)
            {
                CommandEntry command = commands[index];
                string id;

                if (command == null ||
                    string.IsNullOrWhiteSpace(command.Name) ||
                    string.IsNullOrWhiteSpace(command.Command))
                {
                    continue;
                }

                id = string.IsNullOrWhiteSpace(command.Id) ? NewId("cmd") : command.Id.Trim();

                while (usedIds.Contains(id))
                {
                    id = NewId("cmd");
                }

                usedIds.Add(id);

                results.Add(new CommandEntry
                {
                    Id = id,
                    Name = command.Name.Trim(),
                    Command = command.Command.Trim(),
                    RunMode = RunModeCatalog.Normalize(command.RunMode),
                    EnabledOnStart = command.EnabledOnStart,
                    AutoRetry = SanitizeAutoRetry(command.AutoRetry),
                    WorkingDirectory = string.IsNullOrWhiteSpace(command.WorkingDirectory)
                        ? null
                        : command.WorkingDirectory.Trim(),
                    EnvironmentVariables = SanitizeEnvironmentVariables(command.EnvironmentVariables)
                });
            }

            return results.ToArray();
        }

        private static AutoRetryConfig SanitizeAutoRetry(AutoRetryConfig config)
        {
            if (config == null)
            {
                return CreateDefaultAutoRetry();
            }

            return new AutoRetryConfig
            {
                Enabled = config.Enabled,
                MaxAttempts = Math.Max(0, config.MaxAttempts),
                InitialDelaySeconds = Math.Max(1, config.InitialDelaySeconds <= 0 ? 3 : config.InitialDelaySeconds),
                MaxDelaySeconds = Math.Max(1, config.MaxDelaySeconds <= 0 ? 60 : config.MaxDelaySeconds),
                ResetAfterSeconds = Math.Max(1, config.ResetAfterSeconds <= 0 ? 300 : config.ResetAfterSeconds)
            };
        }

        public static AutoRetryConfig CreateDefaultAutoRetry()
        {
            return new AutoRetryConfig
            {
                Enabled = false,
                MaxAttempts = 0,
                InitialDelaySeconds = 3,
                MaxDelaySeconds = 60,
                ResetAfterSeconds = 300
            };
        }

        public static string NewId(string prefix)
        {
            return prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }
    }
}
