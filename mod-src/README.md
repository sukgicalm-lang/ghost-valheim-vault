# Ghost Smelter Control 1.0.1

Valheim 1.0.20용 제련·제분 제어 모드입니다. 기존 Ghost Smelter Control에 풍차 설정만 추가합니다. 원본 FrizzQOL의 MIT 고지를 유지합니다.

게임을 종료하고 Ghost 런처를 열면 이 독립 모드만 자동으로 업데이트됩니다. 런처 재설치나 기존 통합팩 재다운로드는 필요하지 않습니다. 개인 설정 파일은 배포본에 포함하지 않아 기존 값을 유지합니다.

## F1 설정

F1 → Ghost Smelter Control → Windmill

- Seconds = 1: 보리 가루 한 개의 **기준 처리 시간**입니다. 낮추면 빨라집니다. 기존 바람 세기·풍차 가림의 영향은 유지하므로 실제 시간은 달라집니다.
- Barley = 100: 풍차에 저장할 수 있는 보리의 최대 개수입니다. 한 번 클릭의 투입 수량이나 보리 한 개당 생산량을 바꾸는 설정이 아닙니다.

기존 Smelter(Seconds/Ore/Fuel), BlastFurnace(Seconds/Ore/Fuel), CharcoalKiln(Seconds/Wood) 키와 설정값은 그대로 유지합니다. 새 풍차 키를 포함하여 총 10개 설정입니다. F1 변경은 이미 불러온 시설에도 적용합니다. 0 이하를 넣으면 캡처한 원래 프리팹 값으로 복원합니다.

풍차의 바람·회전·가림 계산, 원료 소모, 레시피와 생산물 묶음 배출은 변경하지 않습니다. 입력 한도를 낮춰도 기존 원료를 삭제하지 않습니다. 서버와 함께 플레이하는 이용자는 같은 모드 버전과 설정값을 사용하세요. 이 모드는 설정값을 네트워크로 동기화하는 기능을 추가하지 않습니다.

## 비상 수동 설치

게임을 종료한 뒤 이 ZIP을 압축 해제하지 않고 r2modman의 Import local mod에서 가져옵니다. 기존과 같은 Author Ghost / Name GhostSmelterControl / Version 1.0.1을 사용하여 교체합니다. DLL을 별도 폴더에 중복 복사하지 않습니다. 원본 FrizzQOL Smelter Control은 함께 설치하지 않습니다.

## 검증

사용자가 제공한 실제 게임 DLL(1.0.20)을 확인해 컴파일했습니다. DLL 빌드와 32개 자동 검사 통과. 실제 Valheim/Unity 월드·서버·친구 PC에서는 아직 실행 검증하지 않았습니다. 자세한 범위는 VALIDATION.md를 참고하세요.
