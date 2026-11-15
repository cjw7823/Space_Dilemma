# Space HUD — 씬에서 직접 편집

`Assets/Scenes/SampleScene.unity`의 **Space HUD**와 **Custom Input System**은 씬에 저장된 오브젝트입니다. Play를 누르지 않아도 Hierarchy에서 찾아서 배치·크기·색상·프리팹·텍스트·버튼 이벤트를 수정할 수 있습니다. 실행 중 UI를 새로 생성하지 않습니다.

```text
SampleScene
├ Space HUD
│  ├ HUD Canvas
│  │  ├ Player Controls Panel
│  │  ├ Minimap Frame / Minimap Viewport / Rover Marker / Player Direction
│  │  ├ Objective Panel / Objective Header / Objective Title / Objective Description / Objective Progress
│  │  ├ Clock Panel / Clock Text
│  │  ├ Pause Button
│  │  └ Pause Overlay (기본 비활성)
│  │     ├ Pause Window / Continue·Settings·Quit Game 버튼
│  │     └ Settings Window / 언어·COM 포트·수신 진단
│  └ Minimap Camera
└ Custom Input System
   ├ Solar generator input example
   ├ Oxygen generator input example
   └ Input Examples Canvas (기본 비활성)
```

원하는 오브젝트의 `RectTransform`, `Image`, `TextMeshPro`, `PixelLabel`, `PixelButton` 컴포넌트를 Inspector에서 수정한 뒤 씬을 저장하세요. Pixel UI 레이블은 자식 `Text`의 TMP 컴포넌트에서 글자·폰트·색을 수정합니다. 목표 제목과 설명의 초기 문자열은 씬에 저장된 텍스트를 사용합니다. 한글 목표 글꼴은 Windows에 설치된 맑은 고딕을 사용하는 TMP 에셋이므로 다른 OS나 빌드 환경에서는 한글 글꼴을 별도로 확인해야 합니다.

일시정지 창을 편집할 때는 Hierarchy에서 `Pause Overlay`를 임시로 활성화하고 수정한 뒤 다시 비활성화하세요. `Settings Window`를 편집할 때는 해당 자식도 켭니다. F2 입력 예제 메뉴는 `Input Examples Canvas`를 같은 방식으로 편집합니다.

Esc로 일시정지한 뒤 **Settings / 설정**을 선택하면 언어와 COM 포트를 바꿀 수 있습니다. **Quit Game / 게임 종료**는 실행한 게임을 닫고 Unity Editor에서는 Play를 끝냅니다. 설정 중 Esc는 일시정지 창으로 돌아갑니다.

- English와 한국어 중 메뉴·HUD 안내 언어를 선택합니다. 선택은 다음 실행에도 적용됩니다. 영어 모드는 목표와 COM 오류도 영어로 표시합니다. 한국어 모드에서는 COM, 키 이름, F2 테스트 화면 같은 기술 용어를 영어로 표시할 수 있습니다.
- 감지된 포트 버튼을 눌러 목록을 순서대로 선택하거나 COM을 직접 입력합니다. 전송 속도, 데이터 비트, 패리티, 정지 비트, DTR과 RTS를 입력한 뒤 **연결 / 재연결**을 누르면 현재 실행에 적용되고 다음 실행을 위해 저장됩니다. 기존 자동 연결 설정이 켜져 있으면 저장한 포트를 다음에 자동으로 엽니다.
- 하단에서 연결 상태, 최근 수신 행, 원시 값, 정규화한 입력, 패킷 오류와 순번 누락을 확인할 수 있습니다. 패킷 주입과 Action 추적은 Editor 디버거에서 사용합니다.

## 미니맵

- 카메라가 플레이어를 따라가는 높이는 **Space HUD → Minimap Controller → Camera Height**에서 바꿉니다.
- 지도에 보이는 범위는 **Minimap Camera → Camera → Orthographic Size**에서 바꿉니다. RenderTexture는 `Assets/SpaceHud/SceneAssets/Minimap.renderTexture`입니다.
- 마커를 추가하려면 `Minimap Viewport` 아래에 Image를 직접 배치하거나 `Rover Marker`를 복제하고, `Hud Map Marker`의 Target에 씬 오브젝트를 지정합니다. 범위 밖 마커는 숨깁니다. 마커의 이미지·크기·색은 Inspector에서 편집합니다.
- 카메라는 UI 레이어와 Skybox를 제외하고 별도 RenderTexture에 그립니다. 기존 Main Camera는 변경하지 않습니다.

## 목표와 시간

초기 목표 `태양광 발전소 복구 / 발전기 정상화`는 `EXAMPLE`로 표시되는 테스트용 내용입니다. 실제 임무 시스템은 아직 없습니다. 외부 게임 로직에서는 다음처럼 상태만 갱신할 수 있습니다.

```csharp
using SpaceDilemma.Hud;

HudController.Active.Objective.SetLocalized(
    "Inspect the oxygen unit", "Check the filter.",
    "산소 장치 점검", "필터를 확인하세요.", 1, 3);
```

`HudController.Active`는 씬 오브젝트의 `Awake`에서 설정됩니다. 예시 목표의 영어 텍스트는 Space HUD의 Hud Controller Inspector, 한국어 텍스트는 Objective Panel의 제목과 설명에서 편집합니다. 새 목표는 두 언어를 함께 넘깁니다. 단일 언어 API인 `Set`에 한글 목표를 넘기면 영어 화면에 기본 안내를 표시합니다. 진행도의 초기 숫자와 Pause 입력 컨텍스트는 `Assets/SpaceHud/Resources/SpaceHudSettings.asset`에 있습니다.

시계는 기존 `Moon Environment / LunarEnvironment`의 Sun Angle CSV 시뮬레이션 시간을 읽습니다. **CSV 시간 (시작 후 h)**과 **Sun Hours Per Second**로 시작 시각과 배율을 조절합니다. 0.02는 실제 1초에 게임 72초이며, 기존 자료의 마지막인 48시간에서 멈춥니다.

## 입력과 코드

Esc 또는 Pause 버튼은 일시정지/재개, Tab은 커서 잠금 해제, F2는 입력 예제 메뉴, Backspace는 예제 메뉴의 뒤로 가기입니다. 게임패드/Serial의 Pause 버튼은 아직 지정하지 않았습니다.

- `HudController`: 씬에서 연결한 텍스트·버튼의 게임 상태 갱신.
- `MinimapController`, `HudMapMarker`: 미리 배치한 카메라와 마커의 위치 갱신.
- `HudPauseManager`: 일시정지와 기존 입력 컨텍스트·시간 배율 복원. 버튼의 On Click 연결은 씬에 저장됩니다.
- `ObjectiveModel`: UI와 분리된 목표 데이터.

## 검증

`Tools > Space HUD > Run Play Validation`은 현재 열린 SampleScene을 저장하거나 다시 열지 않고 Editor Play 검사를 수행합니다. 결과와 화면 렌더는 `QA/SpaceHud`에 저장합니다. 가상 입력 및 카메라 렌더 결과와 실제 하드웨어·독립 실행 빌드 검증은 구분합니다.

## 최종 결과 (STEP 6)

SampleScene의 Survival Session/FinalEvaluation과 HUD Canvas/Final Result Overlay를 사용한다. 48시간 종료·조기 사망 시 최종 연구/생존 결과를 표시하며, 설정·ESC 메뉴를 닫아도 종료 상태를 유지한다. 설정과 구조는 Assets/Survival/STEP6.md, 검증은 QA/ResultStep6/VALIDATION.md를 따른다. 이후 UI도 씬에 직접 배치한다.


## 연구 달성도와 방사선 피폭도

SampleScene의 `Space HUD / HUD Canvas / Research Gauge`와 `Radiation Gauge`는 직접 편집할 수 있는 세로 카드다. 파란 카드의 숫자는 시료 채취·보존, 측정장비, 식물 재배의 가중 합산 점수이며 결과 화면에도 같은 계산을 사용한다. 붉은 카드는 누적 피폭량을 게임의 위험 기준 대비 퍼센트로 보여주며 mSv 값도 아래에 표시한다.

Project 창에서 `Assets/Survival/Evaluation Rules.asset`을 열면 Inspector에서 세 미션의 비중을 입력할 수 있다. 예를 들어 40, 35, 25를 입력하면 실제 반영 비율은 40%, 35%, 25%다. 입력 합계가 다르면 같은 상대 비율을 유지하며 100%로 환산한다. 세 값이 모두 0이면 동일 비중을 사용한다. 설정은 다음 Play부터 적용된다. 장치별 데이터 수집 속도는 `Research Data Rules - Test Values.asset`에서 조절한다.
