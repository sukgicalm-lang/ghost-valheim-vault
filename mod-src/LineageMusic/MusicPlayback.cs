using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace GhostLineageMusic {
    internal sealed class MusicPlayback {
        const float FadeSeconds = 0.75f;
        static MusicPlayback current;
        readonly MusicMan manager;
        readonly AudioSource native;
        readonly TrackLibrary library = new TrackLibrary();
        readonly Dictionary<string, float> failed = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        readonly AudioSource[] sources = new AudioSource[2];
        UnityWebRequest request;
        Task<VorbisTrack> decoding;
        CancellationTokenSource decodeCancellation;
        AudioClip prepared;
        string musicRoot, clientMusic, clientSetting, requestedPath, preparedPath;
        float nextScan, nextClientScan, transition, retryAfter, nextContextCheck;
        int active = -1;
        MusicContext desired, playing;
        bool nativeMuted, previousMute, hasContext;

        MusicPlayback(MusicMan manager, AudioSource native) {
            this.manager = manager;
            this.native = native;
        }

        internal static void Detach(MusicMan owner = null) {
            if (current == null || (owner != null && owner != current.manager))
                return;
            current.Reset();
            foreach (var source in current.sources)
                if (source)
                    UnityEngine.Object.Destroy(source);
            current = null;
        }

        void Update(float combatTimer) {
            var plugin = Plugin.Instance;
            var player = Player.m_localPlayer;
            var game = Game.instance;
            if (!plugin.Enabled.Value || !player || player.IsDead() || !game ||
                game.IsShuttingDown() || !ZNet.instance) {
                if (active >= 0 || hasContext)
                    Reset();
                return;
            }
            if (AudioListener.pause)
                return;

            var now = Time.unscaledTime;
            var path = Path.GetFullPath(plugin.MusicFolder.Value);
            if (!StringComparer.OrdinalIgnoreCase.Equals(musicRoot, path)) {
                Reset();
                musicRoot = path;
                nextScan = 0;
            }
            if (now >= nextScan) {
                nextScan = now + 5f;
                try {
                    library.Refresh(musicRoot, plugin.BundledMusic, () => FindClientMusic(now));
                } catch (IOException error) {
                    plugin.Warn("음원 폴더를 읽지 못했습니다. " + error.Message);
                } catch (UnauthorizedAccessException error) {
                    plugin.Warn("음원 폴더를 읽지 못했습니다. " + error.Message);
                } catch (ArgumentException error) {
                    plugin.Warn("리니지 II 경로를 확인해 주세요. " + error.Message);
                }
            }

            bool battle = combatTimer > 0f && library[MusicContext.Battle].HasTracks;
            if (!hasContext || now >= nextContextCheck || battle != (desired == MusicContext.Battle)) {
                nextContextCheck = now + 0.2f;
                var profile = game.GetPlayerProfile();
                bool hasBed = profile != null && profile.HaveCustomSpawnPoint();
                var bed = hasBed ? profile.GetCustomSpawnPoint() : Vector3.zero;
                var position = player.transform.position;
                var context = MusicRules.Select(battle, library[MusicContext.Battle].HasTracks,
                    hasBed, position.x, position.z, bed.x, bed.z, plugin.Radius.Value);
                if (!hasContext || desired != context) {
                    CancelLoad();
                    desired = context;
                    hasContext = true;
                    retryAfter = 0f;
                }
            }

            if (!library[desired].HasTracks) {
                Reset();
                return;
            }

            ReadPrepared(now);
            var source = active < 0 ? null : sources[active];
            bool playingAudio = source && source.isPlaying;
            float remaining = playingAudio ? source.clip.length - source.time : 0f;
            if (prepared && (!playingAudio || desired != playing || remaining <= FadeSeconds)) {
                StartPrepared(now);
                playingAudio = true;
            }
            if (request == null && decoding == null && !prepared && now >= retryAfter)
                LoadNext(now);
            if (!playingAudio && active < 0)
                return;

            float volume = MusicMan.m_masterMusicVolume * plugin.Volume.Value;
            float progress = Mathf.Clamp01((now - transition) / FadeSeconds);
            sources[active].volume = volume * progress;
            int prior = 1 - active;
            if (sources[prior] && sources[prior].clip) {
                if (progress >= 1f)
                    Release(prior);
                else
                    sources[prior].volume = volume * (1f - progress);
            }
            if (sources[active].isPlaying || (sources[prior] && sources[prior].isPlaying))
                MuteNative();
            else
                RestoreNative();
        }

        string FindClientMusic(float now) {
            var plugin = Plugin.Instance;
            var configured = plugin.LineageFolder.Value;
            if (clientSetting != configured || now >= nextClientScan) {
                bool initial = clientSetting == null;
                clientSetting = configured;
                nextClientScan = now + 60f;
                var found = LineageClient.FindMusic(configured, LineageClient.LocalDrives());
                if (initial || !StringComparer.OrdinalIgnoreCase.Equals(clientMusic, found))
                    plugin.LogSource(found != null);
                clientMusic = found;
            }
            return clientMusic;
        }

        void LoadNext(float now) {
            requestedPath = library[desired].Next(path =>
                !failed.TryGetValue(path, out float retry) || retry <= now);
            if (requestedPath == null) {
                retryAfter = now + 1f;
                return;
            }
            var ext = Path.GetExtension(requestedPath).ToLowerInvariant();
            if (ext == ".ogg") {
                string path = requestedPath;
                decodeCancellation = new CancellationTokenSource();
                var token = decodeCancellation.Token;
                decoding = Task.Run(() => VorbisTrack.Read(path, token), token);
                return;
            }
            var type = ext == ".mp3" ? AudioType.MPEG : AudioType.WAV;
            request = UnityWebRequestMultimedia.GetAudioClip(new Uri(requestedPath).AbsoluteUri, type);
            request.timeout = 15;
            request.SendWebRequest();
        }

        void ReadPrepared(float now) {
            if (decoding != null && decoding.IsCompleted) {
                try {
                    var track = decoding.GetAwaiter().GetResult();
                    prepared = AudioClip.Create(Path.GetFileNameWithoutExtension(requestedPath),
                        track.Frames, track.Channels, track.Rate, false);
                    if (!prepared || !prepared.SetData(track.Samples, 0))
                        throw new InvalidDataException("Unity rejected the decoded PCM.");
                    preparedPath = requestedPath;
                } catch (Exception error) {
                    FailedTrack(now, error.Message);
                } finally {
                    decodeCancellation.Dispose();
                    decodeCancellation = null;
                    decoding = null;
                }
            }
            if (request == null || !request.isDone)
                return;
            try {
                if (request.result == UnityWebRequest.Result.Success)
                    prepared = DownloadHandlerAudioClip.GetContent(request);
                if (!prepared || prepared.length <= 0f)
                    FailedTrack(now, request.error);
                else
                    preparedPath = requestedPath;
            } finally {
                request.Dispose();
                request = null;
            }
        }

        void FailedTrack(float now, string error) {
            if (prepared)
                UnityEngine.Object.Destroy(prepared);
            prepared = null;
            failed[requestedPath] = now + 60f;
            Plugin.Instance.Warn($"음원을 읽지 못했습니다: {Path.GetFileName(requestedPath)} {error}");
        }

        void StartPrepared(float now) {
            int index = active == 0 ? 1 : 0;
            Release(index);
            var source = sources[index];
            if (!source) {
                sources[index] = source = manager.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.outputAudioMixerGroup = manager.m_musicMixer;
            }
            source.clip = prepared;
            prepared = null;
            source.volume = 0f;
            source.Play();
            Plugin.Instance.LogTrack(desired, Path.GetFileName(preparedPath));
            active = index;
            playing = desired;
            transition = now;
        }

        void MuteNative() {
            if (!nativeMuted && native) {
                previousMute = native.mute;
                native.mute = true;
                nativeMuted = true;
            }
        }

        void RestoreNative() {
            if (nativeMuted && native)
                native.mute = previousMute;
            nativeMuted = false;
        }

        void Release(int index) {
            var source = sources[index];
            if (!source || !source.clip)
                return;
            source.Stop();
            var clip = source.clip;
            source.clip = null;
            UnityEngine.Object.Destroy(clip);
        }

        void CancelLoad() {
            if (decoding != null) {
                decodeCancellation.Cancel();
                decodeCancellation.Dispose();
                _ = decoding.ContinueWith(task => { _ = task.Exception; },
                    CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                decoding = null;
                decodeCancellation = null;
            }
            if (request != null) {
                request.Abort();
                request.Dispose();
                request = null;
            }
            if (prepared)
                UnityEngine.Object.Destroy(prepared);
            prepared = null;
        }

        void Reset() {
            CancelLoad();
            Release(0);
            Release(1);
            RestoreNative();
            active = -1;
            hasContext = false;
            failed.Clear();
            retryAfter = 0f;
        }

        [HarmonyPatch(typeof(MusicMan), "Update")]
        static class MusicPatch {
            [HarmonyPostfix]
            static void Postfix(MusicMan __instance, AudioSource ___m_musicSource, float ___m_combatTimer) {
                if (!Plugin.Instance || Plugin.Instance.PlaybackFailed ||
                    MusicMan.instance != __instance || !___m_musicSource)
                    return;
                try {
                    if (current == null || current.manager != __instance) {
                        Detach();
                        current = new MusicPlayback(__instance, ___m_musicSource);
                    }
                    current.Update(___m_combatTimer);
                } catch (Exception error) {
                    Detach();
                    Plugin.Instance.PlaybackFailed = true;
                    Plugin.Instance.Warn("배경음악 재생 오류. 발헤임 기본 음악으로 복귀합니다. " + error.Message);
                }
            }
        }

        [HarmonyPatch(typeof(MusicMan), "OnDestroy")]
        static class DestroyPatch {
            [HarmonyPrefix]
            static void Prefix(MusicMan __instance) => Detach(__instance);
        }
    }
}
