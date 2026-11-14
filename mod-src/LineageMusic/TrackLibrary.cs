using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GhostLineageMusic {
    internal enum MusicContext { Town, Field, Battle }

    internal static class MusicRules {
        internal static MusicContext Select(bool inCombat, bool hasBattleTracks, bool hasBed,
            float x, float z, float bedX, float bedZ, float radius) {
            if (inCombat && hasBattleTracks)
                return MusicContext.Battle;
            float dx = x - bedX, dz = z - bedZ;
            return hasBed && dx * dx + dz * dz <= radius * radius ? MusicContext.Town : MusicContext.Field;
        }
    }

    internal sealed class TrackBag {
        readonly Random random;
        readonly List<string> remaining = new List<string>();
        string[] tracks = Array.Empty<string>();
        string last;

        internal TrackBag(Random random) { this.random = random; }

        internal bool HasTracks => tracks.Length != 0;

        internal void Refresh(IEnumerable<string> files) {
            var updated = files.Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
            if (tracks.SequenceEqual(updated, StringComparer.OrdinalIgnoreCase))
                return;
            tracks = updated;
            remaining.Clear();
        }

        internal string Next(Func<string, bool> usable) {
            for (int pass = 0; pass < 2; pass++) {
                remaining.RemoveAll(path => !usable(path));
                if (remaining.Count > 0) {
                    int index = remaining.Count - 1;
                    if (index > 0 && StringComparer.OrdinalIgnoreCase.Equals(remaining[index], last)) {
                        int swap = random.Next(index);
                        var value = remaining[index];
                        remaining[index] = remaining[swap];
                        remaining[swap] = value;
                    }
                    last = remaining[index];
                    remaining.RemoveAt(index);
                    return last;
                }
                remaining.AddRange(tracks.Where(usable));
                for (int i = remaining.Count - 1; i > 0; i--) {
                    int j = random.Next(i + 1);
                    var value = remaining[i];
                    remaining[i] = remaining[j];
                    remaining[j] = value;
                }
            }
            return null;
        }
    }

    internal sealed class TrackLibrary {
        readonly Dictionary<MusicContext, TrackBag> lists;
        internal TrackLibrary() {
            var random = new Random();
            lists = Enum.GetValues(typeof(MusicContext)).Cast<MusicContext>()
                .ToDictionary(context => context, context => new TrackBag(random));
        }

        internal TrackBag this[MusicContext context] => lists[context];

        internal static bool Supported(string path) {
            switch (Path.GetExtension(path).ToLowerInvariant()) {
                case ".ogg": case ".mp3": case ".wav": return true;
                default: return false;
            }
        }

        internal void Refresh(string root, string clientMusic) {
            var clientTracks = clientMusic == null ? Array.Empty<string>() :
                Directory.EnumerateFiles(clientMusic, "*.ogg").ToArray();
            foreach (var entry in lists) {
                var folder = Path.Combine(root, entry.Key.ToString());
                var personal = Directory.Exists(folder)
                    ? Directory.EnumerateFiles(folder).Where(Supported).ToArray() : Array.Empty<string>();
                entry.Value.Refresh(personal.Length > 0 ? personal :
                    clientTracks.Where(file => LineageClient.Classify(file) == entry.Key));
            }
        }
    }
}
