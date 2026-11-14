using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace GhostLineageMusic {
    internal static class LineageClient {
        static readonly string[] GameFolders = { "Lineage2_KR", "Lineage2", "Lineage II", "Lineage2_Classic", "리니지2" };
        static readonly string[] InstallFolders = { "", "Games", "NC", "NCSOFT", "NCSoft", "PURPLE", "NC/PURPLE", "Program Files/NC", "Program Files (x86)/NC", "Program Files/NCSOFT", "Program Files (x86)/NCSOFT" };
        static readonly Regex Town = new Regex(@"^(?:(?:NT|T)\d{2}_(?:F|S\d{2})|NT_.+|BR_MUSIC_TOWN)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        static readonly Regex Field = new Regex(@"^(?:F\d{2}|NF_?\d{2})(?:_(?:F|S\d{2}|NF))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        static readonly Regex Battle = new Regex(@"^(?:B|NB|NNB)\d{2}_(?:F|S\d{2}|SC\d)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static MusicContext? Classify(string path) {
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetExtension(path), ".ogg"))
                return null;
            var stem = Path.GetFileNameWithoutExtension(path);
            if (Town.IsMatch(stem)) return MusicContext.Town;
            if (Field.IsMatch(stem)) return MusicContext.Field;
            if (Battle.IsMatch(stem)) return MusicContext.Battle;
            return null;
        }

        internal static string FindMusic(string configured, IEnumerable<string> roots) {
            if (!string.IsNullOrWhiteSpace(configured)) {
                var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured.Trim()));
                if (Directory.Exists(Path.Combine(path, "music")))
                    return Path.Combine(path, "music");
                return Directory.Exists(path) ? path : null;
            }
            foreach (var game in RegisteredInstallations()) {
                var music = Path.Combine(game, "music");
                if (Directory.Exists(music) && Directory.EnumerateFiles(music, "*.ogg").Any())
                    return music;
            }
            foreach (var root in roots) {
                foreach (var parent in InstallFolders) {
                    foreach (var game in GameFolders) {
                        var music = Path.Combine(root, parent, game, "music");
                        if (Directory.Exists(music) && Directory.EnumerateFiles(music, "*.ogg").Any())
                            return music;
                    }
                }
            }
            return null;
        }

        internal static IEnumerable<string> LocalDrives() =>
            DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed)
                .Select(drive => drive.RootDirectory.FullName);

        static IEnumerable<string> RegisteredInstallations() {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return Array.Empty<string>();
            const string uninstall = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
            var installs = new List<string>();
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine }) {
                ReadLocations(hive, uninstall, installs);
                ReadLocations(hive, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", installs);
            }
            return installs;
        }

        static void ReadLocations(RegistryKey hive, string path, List<string> installs) {
            try {
                using (var entries = hive.OpenSubKey(path)) {
                    if (entries == null)
                        return;
                    foreach (var name in entries.GetSubKeyNames()) {
                        using (var entry = entries.OpenSubKey(name)) {
                            string title = entry?.GetValue("DisplayName") as string;
                            string location = entry?.GetValue("InstallLocation") as string;
                            if (!string.IsNullOrWhiteSpace(location) && Path.IsPathRooted(location) &&
                                (string.Equals(title, "Lineage II", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(title, "Lineage 2", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(title, "리니지2", StringComparison.OrdinalIgnoreCase)))
                                installs.Add(location);
                        }
                    }
                }
            } catch (System.Security.SecurityException) {
                // Some installations only expose the common folders.
            } catch (UnauthorizedAccessException) {
            } catch (IOException) {
            }
        }
    }
}
