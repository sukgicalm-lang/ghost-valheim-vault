using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

[assembly: AssemblyVersion("1.0.3.0")]
[assembly: AssemblyFileVersion("1.0.3.0")]

namespace GhostLineageMusic {
    [BepInPlugin(Id, "Ghost 리니지 II 배경음악", Version)]
    public sealed class Plugin : BaseUnityPlugin {
        const string Id = "ghost.valheim.lineagemusic";
        const string Version = "1.0.3";
        internal static Plugin Instance;
        internal ConfigEntry<bool> Enabled;
        internal ConfigEntry<string> MusicFolder;
        internal ConfigEntry<string> LineageFolder;
        internal ConfigEntry<float> Radius;
        internal ConfigEntry<float> Volume;
        internal string BundledMusic { get; private set; }
        internal bool PlaybackFailed;
        Harmony harmony;

        void Awake() {
            BundledMusic = Path.Combine(Path.GetDirectoryName(Info.Location), "Music");
            Enabled = Config.Bind("음악", "Enabled", true, "상황에 따라 설정된 배경음악을 재생합니다.");
            MusicFolder = Config.Bind("음악", "MusicFolder", Path.Combine(Paths.ConfigPath, "GhostLineageMusic"),
                "Town, Field, Battle 폴더가 들어 있는 경로입니다. OGG, MP3, WAV를 읽습니다.");
            LineageFolder = Config.Bind("음악", "LineageFolder", "",
                "리니지 II 설치 폴더 또는 music 폴더입니다. 비워 두면 로컬 디스크의 일반 설치 경로에서 자동으로 찾습니다.");
            Radius = Config.Bind("음악", "TownRadius", 100f,
                new ConfigDescription("등록한 침대를 중심으로 마을 음악을 듣는 수평 거리(m)입니다.",
                    new AcceptableValueRange<float>(1f, 500f)));
            Volume = Config.Bind("음악", "Volume", 0.8f,
                new ConfigDescription("발헤임의 음악 볼륨에 곱하는 값입니다.",
                    new AcceptableValueRange<float>(0f, 1f)));
            Instance = this;
            Enabled.SettingChanged += (_, __) => PlaybackFailed = false;
            harmony = new Harmony(Id);
            harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo($"리니지 II 배경음악 {Version} 로드 완료");
        }

        void OnDestroy() {
            MusicPlayback.Detach();
            harmony?.UnpatchSelf();
            Instance = null;
        }

        internal void Warn(string message) => Logger.LogWarning(message);
        internal void LogSource(bool found) => Logger.LogInfo(found
            ? "리니지 II 클라이언트 음원을 자동 연결했습니다."
            : "리니지 II 클라이언트 음원을 찾지 못했습니다. 기본 음악 또는 프로필의 지정 음원을 사용합니다.");
        internal void LogTrack(MusicContext context, string name) => Logger.LogInfo($"배경음악 재생 [{context}]: {name}");
    }
}
