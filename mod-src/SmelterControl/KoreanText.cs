using System;
using System.Collections.Generic;
using System.Globalization;

namespace SmelterControl
{
    // Presentation only. These identifiers remain identical to the existing config files.
    internal sealed class KoreanSetting
    {
        internal readonly string Category, Name, Description, Kind, Station;
        internal KoreanSetting(string category, string name, string description, string kind = "value", string station = null)
        { Category = category; Name = name; Description = description; Kind = kind; Station = station; }
    }

    internal static class KoreanText
    {
        internal const string Wardrobe = "ghost.valheim.doomlight", Tweaks = "ghost.valheim.tweaks", Map = "Landoria.MapTeleport";
        internal static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { Plugin.PluginGuid, "Ghost 제련·제분 설정" }, { Wardrobe, "Ghost 외형 꾸미기" },
            { Tweaks, "Ghost 생활·성장 설정" }, { Map, "지도 순간이동 (Ghost 수정)" }
        };
        internal static readonly Dictionary<string, string> FixedNames = new Dictionary<string, string>
        {
            { "Ghost Bow Draw", "Ghost 활 당김 속도 (2배 고정·조절 항목 없음)" },
            { "Ghost Bow Skill", "Ghost 활 9발 강공격 (고정 설정·조절 항목 없음)" }
        };
        internal static readonly Dictionary<string, KoreanSetting> Settings = Build();
        internal static string Id(string guid, string section, string key) { return guid + "|" + section + "|" + key; }
        internal static bool TryGet(string guid, string section, string key, out KoreanSetting text)
        { return Settings.TryGetValue(Id(guid, section, key), out text); }
        private static Dictionary<string, KoreanSetting> Build()
        {
            var items = new Dictionary<string, KoreanSetting>();
            AddStation(items, "Smelter", "1. 일반 제련소", "smelter", "Ore", "광석", true);
            AddStation(items, "BlastFurnace", "2. 용광로", "blastfurnace", "Ore", "광석", true);
            AddStation(items, "CharcoalKiln", "3. 숯가마", "charcoal_kiln", "Wood", "나무", false);
            AddStation(items, "Windmill", "4. 풍차", "windmill", "Barley", "보리", false);
            Add(items, Tweaks, "1 - 발효 시간", "사용", "1. 발효통", "발효 시간 조절 사용", "사용하면 아래 시간에 맞춰 발효합니다. 사용 안 함은 각 발효통의 원래 발효 시간을 사용합니다. 진행 중인 발효의 시작 시각은 초기화하지 않습니다.");
            Add(items, Tweaks, "1 - 발효 시간", "발효 시간(초)", "1. 발효통", "발효 완료까지 걸리는 시간 (분)", "분 단위로 입력합니다. 5는 5분, 10은 10분입니다. 숫자가 작을수록 빨리 끝납니다. 발효 시간 조절을 사용해야 적용됩니다.", "minutes");
            Add(items, Tweaks, "2 - 메긴기요르드", "사용", "2. 무게 벨트", "벨트 무게 한도 조절 사용", "메긴기요르드 벨트를 착용했을 때 아래 무게 한도를 적용합니다. 사용 안 함은 원래 벨트 효과를 사용합니다.");
            Add(items, Tweaks, "2 - 메긴기요르드", "벨트 착용 시 기준 중량", "2. 무게 벨트", "벨트 착용 후 기본 무게 한도", "추가 무게가 아니라 벨트 착용 후의 기준 총 한도입니다. 450이면 기본 중량 300 기준으로 450까지 듭니다. 입력 범위 300~3000. 다른 장비·버프·월드 배율은 별도로 적용됩니다.", "weight");
            Add(items, Tweaks, "3 - 숙련도 경험치", "사용", "3. 숙련도 성장", "경험치 배수 적용", "사용하면 아래 배수만큼 숙련도 경험치를 얻습니다. 사용 안 함은 원래 경험치입니다. 현재 레벨과 사망 시 손실은 바꾸지 않습니다.");
            Add(items, Tweaks, "3 - 숙련도 경험치", "경험치 배율", "3. 숙련도 성장", "숙련도 경험치 (배)", "1은 원래 경험치, 2는 경험치 2배, 5는 경험치 5배입니다. 입력 범위 1~10배. 달리기·무기·농사·제작 등 공통 숙련도 상승 함수를 사용하는 숙련도에 적용됩니다.", "xp");
            Add(items, Map, "Controls", "TeleportKey", "지도 조작", "순간이동할 때 누르는 키", "큰 지도를 연 뒤 선택한 키를 누른 채 목적지를 마우스 왼쪽 버튼으로 누릅니다. 탐험한 지역만, 현재 위치에서 수평거리 100m 이상일 때 이동할 수 있습니다.", "mapkey");
            Add(items, Wardrobe, "General", "Enabled", "1. 기본 설정", "캐릭터 외형 꾸미기 사용", "선택한 의상 외형을 표시합니다. 사용 안 함은 원래 장비 외형을 표시합니다. 실제 장비 능력치는 바뀌지 않습니다.");
            Add(items, Wardrobe, "General", "LocalPlayerOnly", "1. 기본 설정", "내 캐릭터에만 외형 표시", "사용하면 내 캐릭터만 꾸밉니다. 사용 안 함은 다른 플레이어에게도 선택된 외형을 표시합니다.");
            Add(items, Wardrobe, "General", "UnlockOnLogin", "1. 기본 설정", "접속할 때 외형 목록 열기", "접속하면 이 모드의 의상을 발견한 외형 목록에 추가합니다. 이미 선택한 외형은 유지합니다.");
            Add(items, Wardrobe, "Appearance", "BodyShape", "2. 체형과 망토", "의상에 맞출 체형", "발헤임 체형은 기본 게임 체형에 의상을 맞춥니다. 원작 체형은 의상별 원작 비율을 사용합니다. 둠 경갑은 이 선택과 관계없이 원작 비율을 유지합니다. 실제 캐릭터 능력치와 충돌 크기는 바꾸지 않습니다.", "shape");
            foreach (var p in new[] { new[] {"OriginalScaleDoom", "둠"}, new[] {"OriginalScaleBerserker", "광전사"}, new[] {"OriginalScaleSkullKnight", "해골기사"}, new[] {"OriginalScaleLordEater", "군주 탐식자"} })
                Add(items, Wardrobe, "Appearance", p[0], "2. 체형과 망토", p[1] + " 이전 크기 설정 (현재 미사용)", "이전 버전과의 호환을 위해 보관한 값입니다. 현재 버전은 키를 자동으로 맞추므로 이 숫자를 바꿔도 외형이 달라지지 않습니다.", "inactive");
            Add(items, Wardrobe, "Appearance", "AnimateCapes", "2. 체형과 망토", "광전사·해골기사 망토 움직임", "사용하면 별도로 선택한 광전사·해골기사 망토가 움직입니다. 사용 안 함은 망토를 고정된 모양으로 표시합니다.");
            Add(items, Wardrobe, "Fitting", "Thickness", "3. 의상 맞춤", "갑옷 두께 (배)", "발헤임 체형에 맞추는 갑옷 두께입니다. 1은 보정 없음, 1.03은 약 3% 두껍게, 0.9는 약 10% 얇게 표시합니다. 입력 범위 0.75~1.5배.", "ratio");
            Add(items, Wardrobe, "Fitting", "NeckWidth", "3. 의상 맞춤", "투구 아래 목 너비 (배)", "1은 원래 너비, 0.9는 약 10% 좁게 표시합니다. 입력 범위 0.75~1배. 외형의 목 윤곽만 바꾸며 실제 게임 뼈대는 유지합니다.", "ratio");
            Add(items, Wardrobe, "Fitting", "HideCoveredBody", "3. 의상 맞춤", "갑옷 안쪽 몸 겹침 숨기기", "사용하면 갑옷 안쪽에서 몸이 겹쳐 보이는 부분을 숨깁니다. 사용 안 함은 몸을 그대로 표시합니다.");
            Add(items, Wardrobe, "Tyrael Wings", "UseOriginalTyraelEffects", "4. 티리엘 날개", "원작 자료 기반 날개·옷 효과", "사용하면 원작 자료를 이용한 티리엘 효과를 표시합니다. 사용 안 함은 이전 방식의 효과로 돌아갑니다. 날개의 움직임과 옷의 빛 흐름은 아래 별도 항목에서 조절합니다.");
            Add(items, Wardrobe, "Tyrael Wings", "UseWearableTyraelWings", "4. 티리엘 날개", "플레이어용 티리엘 날개 사용", "사용하면 플레이어용 날개가 달리기·회전·바람에 반응합니다. 사용 안 함은 이전 캐릭터용 날개 모델을 사용합니다. 원작 자료 기반 효과를 사용해야 적용됩니다.");
            Add(items, Wardrobe, "Tyrael Wings", "AnimateTyraelWings", "4. 티리엘 날개", "티리엘 날개 움직임과 무늬 흐름", "사용하면 날개가 움직이고 빛 무늬가 흐릅니다. 사용 안 함은 기본 자세로 멈춥니다. 이전 날개 모델에서는 원본 동작 재생을 조절합니다.");
            Add(items, Wardrobe, "Tyrael Wings", "TyraelWingSize", "4. 티리엘 날개", "날개 크기 (배)", "1은 기준 크기, 1.5는 1.5배 크게, 0.5는 절반 크기입니다. 입력 범위 0.5~1.75배. 화면에 보이는 날개만 바뀝니다.", "ratio");
            Add(items, Wardrobe, "Tyrael Wings", "TyraelWingBrightness", "4. 티리엘 날개", "날개 빛 밝기 (배)", "1은 기준 밝기, 1.8은 밝기 설정 1.8배입니다. 숫자가 클수록 밝아집니다. 입력 범위 0.5~3배. 옷의 빛 밝기와 별개입니다.", "ratio");
            Add(items, Wardrobe, "Tyrael Wings", "TyraelWingOpacity", "4. 티리엘 날개", "날개 빛 농도 (%)", "백분율로 입력합니다. 48은 48%, 100은 가장 진하게 표시합니다. 숫자가 작을수록 투명하게 보입니다. 입력 범위 15~100%.", "percent");
            Add(items, Wardrobe, "Tyrael Energy", "AnimateTyraelEnergy", "5. 티리엘 옷의 빛", "어깨·팔의 빛 무늬 흐름", "사용하면 옷의 어깨·팔 무늬가 흐릅니다. 사용 안 함은 무늬를 첫 모습에서 멈춥니다.");
            Add(items, Wardrobe, "Tyrael Energy", "TyraelEnergySpeed", "5. 티리엘 옷의 빛", "옷의 빛 흐름 속도 (배)", "1은 기본 속도, 2는 2배 빠르게, 0.5는 절반 속도입니다. 입력 범위 0.1~3배. 생산 속도와는 관계없는 외형 효과입니다.", "ratio");
            Add(items, Wardrobe, "Tyrael Energy", "TyraelEnergyBrightness", "5. 티리엘 옷의 빛", "옷의 빛 밝기 (배)", "1은 기준 밝기, 1.5는 밝기 설정 1.5배입니다. 입력 범위 0.25~3배. 날개 빛 밝기는 별도 항목에서 조절합니다.", "ratio");
            Add(items, Wardrobe, "Rendering", "Shader", "6. 고급 외형 설정", "의상 재질 표시 방식", "기본값은 Custom/Creature입니다. 재질을 그리는 게임 내부 이름이므로 보통 바꿀 필요가 없습니다. 존재하지 않는 이름은 호환 재질로 대체됩니다.", "shader");
            Add(items, Wardrobe, "Diagnostics", "DumpInventoryOnce", "7. 문제 확인", "현재 인벤토리를 로그에 한 번 기록", "음식이 사라지거나 보이지 않을 때 사용합니다. 켜면 현재 아이템 이름·수량·인벤토리 칸을 로그에 한 번 기록한 뒤 자동으로 꺼집니다. 아이템이나 저장 데이터는 바꾸지 않습니다.");
            return items;
        }
        private static void Add(Dictionary<string, KoreanSetting> items, string guid, string section, string key, string category, string name, string description, string kind = "value")
        { items.Add(Id(guid, section, key), new KoreanSetting(category, name, description, kind)); }
        private static void AddStation(Dictionary<string, KoreanSetting> items, string section, string category, string station, string inputKey, string inputName, bool fuel)
        {
            string wind = station == "windmill" ? " 풍차는 바람 세기와 주변 가림에 따라 실제 생산이 느려집니다. 배수는 같은 바람 조건에서 비교한 값입니다." : "";
            items.Add(Id(Plugin.PluginGuid, section, "Seconds"), new KoreanSetting(category, "생산 속도 (원래 속도 기준)", "원래 속도는 1배입니다. 2배·5배·10배 버튼이나 직접 입력한 배수를 적용합니다. 예: 10배는 원래보다 처리 시간이 약 10분의 1입니다. 게임의 처리 간격과 묶음 배출 때문에 정확히 매초 한 개가 나오지는 않습니다." + wind, "speed", station));
            Add(items, Plugin.PluginGuid, section, inputKey, category, inputName + " 최대 보관량 (개)", "한 번에 안에 보관할 수 있는 " + inputName + "의 최대 개수입니다. 100이면 최대 100개를 넣어 둘 수 있습니다. 생산량이나 클릭 한 번에 넣는 개수가 아닙니다. 0 이하는 게임의 원래 보관량을 사용합니다.", "count");
            if (fuel) Add(items, Plugin.PluginGuid, section, "Fuel", category, "석탄 최대 보관량 (개)", "연료로 보관할 수 있는 석탄의 최대 개수입니다. 100이면 최대 100개를 넣어 둘 수 있습니다. 생산물 한 개에 드는 석탄 양은 바뀌지 않습니다. 0 이하는 원래 보관량을 사용합니다.", "count");
        }
        internal static bool Positive(float value) { return value > 0 && !float.IsNaN(value) && !float.IsInfinity(value); }
        internal static float Speed(float seconds, float baseline) { return Positive(seconds) && Positive(baseline) ? baseline / seconds : 1f; }
        internal static bool TrySeconds(float baseline, float multiplier, out float seconds)
        { seconds = Positive(baseline) && Positive(multiplier) ? baseline / multiplier : 0; return Positive(seconds); }
        internal static string Number(float value) { return value.ToString("0.###", CultureInfo.InvariantCulture); }
        internal static string SpeedLabel(float seconds, float baseline)
        {
            if (!Positive(seconds)) return "원래 속도 (1배)";
            if (!Positive(baseline)) return "월드 입장 후 원래 속도와 비교합니다";
            float speed = Speed(seconds, baseline);
            return Math.Abs(speed - 1f) < 0.0001f ? "원래 속도 (1배)" : "약 " + Number(speed) + "배 속도";
        }
    }
}
