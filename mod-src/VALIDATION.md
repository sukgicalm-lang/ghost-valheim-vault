# 검증 — Ghost 제련·제분 설정 1.0.3

- 사용자 제공 실제 게임 DLL의 SHA256: 94998885b103d6e5f5480123eaee0a093e82af2ddeeee37536c15bf555f24f3a. Version 초기화 코드가 GameVersion(1,0,20)을 생성함을 검사했습니다.
- 실제 Windmill.Start/GetPowerOutput, Smelter.UpdateSmelter, ZNetScene의 등록 구조를 검사했습니다. 기존 게임 패치 4개와 Smelter 필드 3개 쓰기 범위는 동일합니다. 바람·가림·생산량·연료 소모·외형·전투 계산은 수정하지 않았습니다.
- 실제 ConfigurationManager 1.1.27 DLL의 ConfigSettingEntry 생성자와 SettingEntryBase 표시 속성을 확인했습니다. 선택적 F1 연동은 설정 표시 객체와 설정 없는 활 모드의 목록 문구만 바꿉니다. 원본 플러그인 메타데이터나 설정 키는 덮어쓰지 않습니다. ConfigurationManager가 없으면 게임 기능만 로드됩니다.
- suite 1.0.40의 실제 Ghost Wardrobe·Ghost Valheim Tweaks·Ghost 수정 MapTeleport DLL과 새 제련 DLL에서 저장되는 설정을 추출해 40개 전부에 한글 제목·분류·설명이 있음을 검사했습니다. 다른 제작자나 알 수 없는 새 키는 그대로 둡니다. suite DLL은 수정하거나 재배포하지 않았습니다.
- SDK 8.0.427 Microsoft Roslyn, 공식 net48 참조 어셈블리, 실제 게임·Unity·BepInExPack_Valheim 5.4.2360 어셈블리로 net48 DLL을 최적화·결정적 빌드했습니다. 경고·오류는 없습니다. TargetFramework 메타데이터는 .NETFramework v4.8입니다. 이 실행 환경의 MSBuild 프로세스 조회 제한으로 Roslyn을 직접 호출했습니다.
- 자동 검사 51개: 기존 생산 동작·게임 구조·설정 키·기본값 32개, 한글 범위·배수·연동 API 10개, 메뉴 버튼·입력·기본값·잠금·설정 유지 9개. 실제 xUnit 단언을 포함한 동일 프로세스 실행기를 사용했습니다.
- 설정 표시 속성은 실제 F1처럼 상위 클래스에 비공개 setter를 둔 시험 구조에서 검사합니다. 일반 상속 반사 조회로 setter가 누락되는 문제를 회귀 검사합니다.
- 메뉴 검사는 시험용 IMGUI API를 별도 어셈블리로 컴파일해 실제 MenuGui의 반사 호출과 실제 KoreanMenu 코드를 실행했습니다. Unity 네이티브 렌더링은 실행하지 않았습니다. 시험용 GUI DLL, 게임·Unity·BepInEx 참조 DLL, 개인 cfg는 설치 ZIP에 넣지 않았습니다.
- Seconds/Ore/Fuel/Wood/Barley 키와 기본 저장값은 동일합니다. 배수·분·퍼센트는 화면에서 변환하고 사용자가 적용할 때만 기존 단위로 저장합니다. 읽기만 하거나 반올림된 현재 값을 그대로 적용하면 원래 정밀도도 유지합니다. 원래 속도는 기존의 0 이하 복원 의미를 사용합니다.
- 실제 Valheim 화면·네이티브 Unity 시뮬레이션·전용 서버·친구 PC에서는 실행하지 않았습니다. GUI 시험은 실제 화면의 시각 검증을 대신하지 않습니다. 원래 프리팹 기준은 런타임에 캡처하며 월드 입장 전에는 임의로 배수를 추정하지 않습니다.
