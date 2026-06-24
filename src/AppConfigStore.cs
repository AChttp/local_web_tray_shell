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

            using (FileStream stream = new FileStream(AppPaths.ConfigPath, FileMode.Create, FileAccess.Write))
            {
                DataContractJsonSerializer serializer =
                    new DataContractJsonSerializer(typeof(AppConfig));
                serializer.WriteObject(stream, config);
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
                GlobalHotkey = SanitizeHotkey(config.GlobalHotkey)
            };
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
                Uri uri;
                string normalizedUrl;
                string name;
                string id;

                if (site == null || string.IsNullOrWhiteSpace(site.Url))
                {
                    continue;
                }

                if (!Uri.TryCreate(site.Url.Trim(), UriKind.Absolute, out uri))
                {
                    continue;
                }

                if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                normalizedUrl = uri.AbsoluteUri;

                if (uniqueSites.ContainsKey(normalizedUrl))
                {
                    continue;
                }

                name = string.IsNullOrWhiteSpace(site.Name)
                    ? uri.Host + (uri.IsDefaultPort ? string.Empty : ":" + uri.Port)
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
                    AutoRetry = SanitizeAutoRetry(command.AutoRetry)
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
