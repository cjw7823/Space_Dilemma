# Custom Input System — Space_Dilemma

키보드·마우스, Unity가 Gamepad로 인식하는 HID 컨트롤러, USB Serial 패널을 **같은 Unity Input Actions**로 사용합니다. 신규 구현과 설정은 이 폴더에 있으며, 기존 `MoonBasePlayer.cs`에만 작은 연결 코드를 추가했습니다. WPF와 Arduino 펌웨어는 변경하지 않았습니다.

## 1. 시작과 현재 씬

- 실제 작업 씬은 `Assets/Scenes/SampleScene.unity`입니다. 요청문에 나온 `Assets/New Scene.unity`는 현재 프로젝트에 없습니다.
- Play 시 `Resources/CustomInputSettings.asset`를 읽고, SampleScene의 기존 PlayerInput에 런타임 복제 Action Asset을 연결합니다. 원래 씬·프리팹·Starter Assets Input Actions 파일을 저장하거나 덮어쓰지 않습니다. 종료 시 원래 PlayerInput 설정을 복원합니다.
- 키보드 조작은 기존과 같습니다. Game 뷰 클릭으로 조작 시작, WASD/방향키 이동, 마우스 시선, Space 점프, Shift 달리기, T 로버 탑승/하차, Esc 커서 해제입니다. 클릭에 필요한 원래 포커스 정책도 보존합니다.
- 게임패드: 왼쪽 스틱 이동, 오른쪽 스틱 시선, South(A/×) 점프, West(X/□) 상호작용/로버, 왼쪽 트리거 달리기. 조작 입력이 있으면 마우스를 클릭하지 않아도 플레이어 조작을 시작할 수 있습니다.
- **F2 / Gamepad Start / Serial 논리 button2**로 예제 메뉴를 엽니다. 실제 시설 운영 시스템이 현재 씬에 없으므로 Solar/Oxygen 예제는 입력과 UI 값 전달만 보여 줍니다. 실제 전력·산소 계산을 구현한 것이 아닙니다.

## 2. 구조와 선택 근거

```text
Keyboard / Mouse ───────────────────────────────┐
HID Gamepad ──────────────────────────────────┤
COM → background reader → LF decoder → parser  │
  → calibration → SerialController InputDevice ┤
                                              ↓
           existing PlayerInput + runtime UnifiedInput Actions
                                              ↓
                InputContextStack (priority + modal restoration)
                       ├ Player → MoonBasePlayer → Starter Assets / Rover
                       ├ UI → InputSystemUIInputModule → EventSystem
                       └ Facility → FacilityInputReceiver → UnityEvent<float>
```

| 후보 | 판단 |
| --- | --- |
| 전용 InputDevice | 채택. `SerialController`에 Vector2 2개, 노브, 슬라이더, 버튼 4개를 정의해 HID의 Gamepad 타입·식별자와 구분합니다. |
| 가상 Gamepad | 노브·슬라이더를 가짜 트리거 등에 끼워 넣지 않고 실제 Gamepad와 별도 binding을 설정하기 위해 사용하지 않았습니다. |
| 독립 Action 시스템 | Action/Binding/Processor/EventSystem은 기존 Unity 기능을 재사용합니다. |

- `Runtime/SerialProtocol.cs`: 새 필드 순서 파싱과 LF 조립. Unity 게임 로직을 모릅니다.
- `Runtime/WindowsSerialTransport.cs`: 작은 `ISerialTransport` 인터페이스와 Windows COM 구현. 네이티브 Windows API를 사용해 추가 DLL이나 유료 라이브러리를 요구하지 않습니다.
- `Runtime/SerialInputBackend.cs`: 백그라운드 읽기, FIFO, 검증/정규화, SEQ/타임아웃/재연결, 가상 장치 이벤트 큐. 수신 스레드는 Unity 객체를 조작하지 않습니다.
- `Runtime/InputHub.cs`: 실제 PlayerInput의 Action 인스턴스, 장치 페어링, 컨텍스트 적용, 상태 조회, 진단 이벤트.
- `Runtime/InputContextStack.cs`, `InputContextProfile.cs`: 우선순위와 토큰 기반 중첩/복원.
- `Runtime/FacilityInputReceiver.cs`: 시설의 논리 액션을 Inspector UnityEvent로 연결합니다.
- `Runtime/UiSelectionScroll.cs`: 비포인터 스틱 스크롤을 선택된 UI의 EventSystem 계층으로 보냅니다. 마우스 휠은 InputSystemUIInputModule이 처리합니다.
- `Runtime/CustomInputBootstrap.cs`, `CustomInputDemoPanel.cs`: SampleScene 자동 설치와 숨겨진 예제 메뉴.
- `Actions/UnifiedInput.inputactions`: Unity 기본 Actions Editor로 수정하는 실제 binding 목록.
- `Profiles/`, `Resources/`: 컨텍스트·통신·보정 기본 에셋.
- `Editor/`: 설정 생성, 디버거, 회귀 검사. 프로덕션 빌드에는 포함되지 않습니다.

## 3. Serial 프로토콜과 설정

```text
S,SEQ,K,S1,JX1,JY1,JX2,JY2,B\n
예: S,42,750,100,500,500,510,490,5\n
```

표시한 `\n` 대신 실제 LF 바이트를 전송합니다. ASCII, 쉼표 구분, 9필드 고정이며 CRLF도 허용합니다. 기본 최대 길이는 LF 포함 96바이트입니다. 초과/잘못된 ASCII는 다음 LF까지 폐기해 경계를 회복합니다. 잘못된 숫자·필드 개수·범위는 게임에 전달하지 않습니다.

| 필드 | 기본 가정 | 변경 위치 |
| --- | --- | --- |
| SEQ | 0~65535, 순환 | Settings → Serial → Maximum Sequence |
| K, S1, JX1, JY1, JX2, JY2 | 각 0~1000 | Settings → Calibration → 축별 Minimum/Maximum |
| B | 0~15 | Maximum Button Mask, Button Bits / Button Enabled |
| 조이스틱 중립 | X/Y 각각500 | 축별 Center |
| 축 방향 | raw가 커지면 + | 축별 Invert; Look의 화면 Y 반전은 binding Processor에도 있음 |
| 노브 특성 | 절대 위치(0~1) | Knob Interpretation: AbsolutePosition / DeltaBetweenPackets, Knob Wraps |
| 주기 | 확정하지 않음 | 예상 주기보다 넉넉한 Input Timeout 설정 |

이 값들은 확인 전의 초기값입니다. 물리 버튼의 의미, 실제 중립점, 노브가 가변저항인지 엔코더인지, 보드별 UART 동작을 확정한 명세가 아닙니다. DeltaBetweenPackets는 **수신 위치 차이/설정 범위**이며 회전 속도/시간 단위가 아닙니다. 펌웨어가 증분 자체를 보내는 경우에는 파서/보정 규약을 합의해야 합니다.

### COM 연결

1. `Tools > Custom Input > Debugger`를 열고 SampleScene을 Play합니다.
2. Refresh COM ports 후 선택하거나 COM3처럼 직접 입력합니다.
3. 기본은 115200 / 8 / None / One, Flow Control 없음, DTR ON/RTS OFF입니다. 포트 이름은 비어 있고 **자동 연결 기본 OFF**라 다른 장치를 임의로 열지 않습니다.
4. Connect / reconnect를 누릅니다. Arduino IDE의 Serial Monitor 등 포트 점유 프로그램은 닫습니다. 변경된 통신 설정은 재연결 때 적용됩니다.
5. 기본 Input Timeout 1초, 시작 유예 3초, 재연결 간격 1초이며 모두 설정 가능합니다. 유효한 S가 오지 않으면 버튼/축을 0으로 리셋하고 연결을 재시도합니다. 형식 오류만 계속 오는 경우에도 정상 입력 타임아웃이 적용됩니다.
6. Disconnect는 재시도를 중단하고 입력을 리셋합니다. 포트 실패는 상태/오류 카운터로 표시하며 HID/키보드는 계속 사용할 수 있습니다.

WHO/ID 조회와 LED 송신은 없습니다. 펌웨어는 WHO를 기다리지 않고 새 형식의 S를 전송해야 합니다. 이 변경된 필드 순서는 과거 WPF의 슬라이더3개 프로토콜과 다르며 WPF는 이 작업에서 수정하지 않았습니다.

현재 COM 백엔드는 **Windows Editor/Windows Standalone**용입니다. WebGL에서는 브라우저가 직접 COM을 제공하지 않습니다. 다른 OS는 `ISerialTransport` 구현이 추가로 필요합니다. 키보드·Gamepad·Serial 가상 입력/Actions 구조는 COM 백엔드와 분리되어 있습니다.

### 전송 순서와 안전성

수신 스레드가 LF 행을 FIFO에 넣고, 메인 스레드 `InputSystem.onBeforeUpdate`가 모두 처리한 뒤 `QueueStateEvent`를 입력 시스템에 넣습니다. 같은 게임 프레임에 button down/up이 들어와도 둘 다 Action callback으로 전달합니다. 최신 1개 패킷으로 합치지 않습니다. 게임 로직에서 모든 반복 눌림을 개별 작업으로 처리하려면 `ActionTriggered`의 Performed 이벤트를 구독하세요. `WasPressed`는 프레임에 한 번만 필요한 점프용입니다.

SEQ 불연속은 카운트하고 정상 패킷은 수신 순서대로 계속 처리합니다. 이를 무조건 패킷 손실 수로 단정하지 않습니다(보드 리셋/중복도 가능). 큐 4096행 초과는 오류로 연결을 리셋합니다. 장치 해제/타임아웃은 0 상태 이벤트를 보내 유지 중인 액션을 해제합니다.

## 4. HID·키보드와 동시 사용 및 충돌 정책

Unity가 `Gamepad`로 인식하는 Xbox 호환/지원 HID 컨트롤러를 그대로 사용합니다. 드라이버가 임의 HID를 `Joystick`으로만 인식하는 경우 해당 장치 control path를 Actions Editor에서 추가해야 하며, 모든 vendor-defined HID가 자동으로 같은 버튼 배치를 갖는다고 보장하지 않습니다.

InputHub는 **단일 플레이어**에 Keyboard/Mouse/Gamepad/SerialController를 함께 페어링하고 `neverAutoSwitchControlSchemes=true`로 독점 자동 전환을 막습니다. 기존 control scheme 이름은 호환성을 위해 보존하되 런타임 활성 scheme 필터는 비웁니다. 다른 InputUser가 소유한 장치는 가져오지 않습니다. 여러 PlayerInput이 있는 씬은 자동 설치하지 않으며 명시적 플레이어 할당/정책이 필요합니다.

| 액션 유형 | 정책 |
| --- | --- |
| 버튼 | Unity `Button` 액션의 가장 강한 control 상태. 다른 장치가 계속 누르고 있으면 한 장치의 해제로 전체 해제되지 않음. |
| 이동·시선·탐색 Vector2 | Unity `Value` 액션의 최대 입력 강도 사용. 합산하지 않음. 같은 강도는 현재 구동 control을 유지하며 첫 선택은 입력 이벤트 처리 순서를 따름. 고정된 Serial 우선권은 없음. |
| 노브·슬라이더 절대값 | `PassThrough` float 액션. **값이 마지막으로 변한 source**가 소유하며 0으로의 변화도 새 입력임. 다른 장치의 큰 절대값이 계속 이기지 않음. |

마우스 Look은 원래 Pointer delta Processor와 `MoonBasePlayer.mouseSensitivity`를 유지합니다. Gamepad/Serial Look은 정규화된 스틱 × Settings.Look Degrees Per Second를 모터에 전달하고 모터가 deltaTime을 곱합니다. 모터의 작은 값 무시 검사를 통과한 뒤 시간 배율이 적용되므로 아주 높은 프레임 속도에서도 회전이 사라지지 않습니다. 로버 시선도 같은 초당 회전량을 사용합니다.

## 5. Input Context와 API

기본 컨텍스트는 Gameplay(priority0/map Player), Facility(priority50), SolarGenerator/OxygenGenerator(priority50), UI(priority100)입니다. 같은 우선순위에서는 마지막으로 Push한 토큰이 이깁니다. Pop은 지정한 토큰만 제거하므로 UI를 닫으면 이전 시설 또는 Gameplay가 복원됩니다.

```csharp
using SpaceDilemma.CustomInput;

InputHub hub = InputHub.Active;
int facilityToken = hub.PushContext(hub.settings.solarGenerator);
int menuToken = hub.PushContext(hub.settings.ui); // facility 위에 UI
hub.PopContext(menuToken);                       // SolarGenerator 복원
hub.PopContext(facilityToken);                   // Gameplay 복원
```

스택 조회는 즉시 갱신되며 Action Map 교체는 Hub의 다음 Update에서 플레이어 Update 전에 적용합니다. 입력 callback 안에서 Action Map을 변경하는 재진입을 피합니다. 실제 적용은 `ContextChanged` 이벤트와 Debugger의 active maps/trace로 추적합니다. Global은 계속 활성, 나머지는 우승 컨텍스트 map 하나만 활성화합니다.

컨텍스트 전환 시 눌려 있던 버튼이 새 Submit으로 다시 눌린 것으로 취급되지 않게 버튼의 initial-state check를 끕니다. 방향 입력은 중립에 돌아온 뒤 다시 활성화합니다. 절대값 액션은 최초 상태 검사 없이 **다음 실제 값 변화**부터 전달하므로 새 시설을 선택한 순간 이전 슬라이더 값이 덮어쓰지 않습니다. UI에서는 캐릭터/로버 이동을 비우고 커서를 풀며 이전 캡처 상태를 복원합니다. 연결 해제는 컨텍스트 스택을 변경하지 않습니다.

```csharp
void OnEnable() => InputHub.Active.ActionTriggered += Handle;
void OnDisable() { if (InputHub.Active) InputHub.Active.ActionTriggered -= Handle; }
void Handle(ActionSignal signal)
{
    if (signal.map == "Player" && signal.action == "Interact" &&
        signal.phase == UnityEngine.InputSystem.InputActionPhase.Performed)
    {
        // 논리적 상호작용 처리. 실제 장치 종류를 확인할 필요 없음.
    }
}
```

## 6. Inspector 매핑과 확장 예제

`Resources/CustomInputSettings.asset`에서 통신·보정과 기본 Context Profile을 편집합니다. `Actions/UnifiedInput.inputactions`는 Unity 기본 Input Actions Editor에서 엽니다. Gamepad와 `<SerialController>/...`는 별도 binding이므로 독립적으로 수정할 수 있습니다. binding을 비우거나 Context Profile의 override path를 빈 문자열로 두면 해당 연결을 비활성화할 수 있습니다.

- joystick1/joystick2는 Vector2, knob/slider는 Axis, button0~3은 Button입니다.
- Serial 범위·중립·보정은 Calibration에서, 액션별 감도·반전은 Unity의 Scale/Invert/Deadzone Processor에서도 설정할 수 있습니다. 두 곳에 중복 적용하면 두 번 보정되므로 의도를 구분하세요.
- Context Profile의 Binding Overrides는 action 이름과 binding GUID(또는 액션 내 bindingIndex), 새 path/Processor로 특정 컨텍스트만 바꿉니다. GUID는 Actions Editor/텍스트에서 확인할 수 있습니다.
- Play 중 Hub.settings는 원본 에셋의 **복제본**입니다. Debugger에서 바꾸는 값은 플레이 종료 시 사라집니다. 영구 설정은 원본 Settings 에셋에 저장합니다.

### 새 시설: OxygenGenerator

제공된 OxygenGenerator map은 Serial slider와 Gamepad rightTrigger를 `OxygenOutput`에 연결합니다. 시설 GameObject에 `FacilityInputReceiver`를 추가하고 Context=OxygenGenerator, Scalar Action=OxygenOutput, Value Changed에 실제 산소 장치의 설정 메서드를 연결합니다. 시설 선택 시 `Activate()`, 선택 해제 시 `Deactivate()`를 호출합니다. UI Slider의 onValueChanged에는 receiver의 `SetValueFromUi(float)`를 연결할 수 있습니다.

```csharp
// Inspector로 설정한 receiver. 프로토콜 바이트나 COM은 시설 코드에서 읽지 않습니다.
public FacilityInputReceiver oxygenInput;
public void SelectFacility() => oxygenInput.Activate();
public void LeaveFacility() => oxygenInput.Deactivate();
```

### 새 컨텍스트: ResearchConsole

1. Input Actions Editor에서 Facility map을 복제해 ResearchConsole로 이름을 정하고 AdjustValue 등 필요한 논리 액션 이름을 지정합니다.
2. Create > Custom Input > Context Profile: Context Id/Action Map=ResearchConsole, Priority=50, Blocks Gameplay=true.
3. 해당 map에 `Select`(Vector2), `Confirm`/`Cancel`(Button)을 두면 예제와 같은 EventSystem 탐색을 사용할 수 있습니다. 일반 UI 이름은 UI map의 `Navigate`/`Submit`/`Cancel`입니다.
4. 위 Push/Pop API로 이 프로필을 사용합니다. 기존 serial backend/플레이어 코드는 수정할 필요가 없습니다.
5. 동일 slider가 다른 액션에 연결되게 binding path `<SerialController>/slider`를 새 액션에 지정합니다. Gamepad trigger도 별도로 지정합니다.

## 7. 디버그 도구와 검증

`Tools > Custom Input > Debugger`는 연결 장치, Serial 상태/포트/오류, 현재 컨텍스트/활성 map/스택, 원본 축/버튼 마스크, 정규화 값, 실제 Action 발생 trace를 표시합니다. 반복 Debug.Log 출력을 사용하지 않습니다.

실제 패널이 없으면 Play에서 **Start isolated serial simulation**을 누른 뒤 아래 세 줄을 한 번에 주입합니다.

```text
S,100,500,0,500,500,500,500,0
S,101,500,0,500,500,500,500,2
S,102,500,0,500,500,500,500,0
```

Player/Jump의 Performed와 Canceled가 모두 trace에 나타납니다. Simulation은 실제 COM을 닫고 전용 SerialController에 입력하며 HID를 흉내내는 Gamepad를 만들지 않습니다. 이 기능은 Unity Editor 도구에만 있고 WPF에 제거했던 모의 버튼을 되살린 것이 아닙니다.

`Tools > Custom Input > Run All Checks (Editor + Play)`는 파서·보정·가짜 전송 계층 오류/재연결과 실제 SampleScene의 PlayerInput/가상 Keyboard/Mouse/Gamepad/Serial/UI를 검사합니다. 결과는 `Tests/Results/`에 저장합니다. 검사는 Play에 들어갔다 나옵니다. 작업 중 씬을 저장하고 실행하세요. 자동 검사는 물리 HID/Arduino USB 통신을 검증한 것으로 취급하지 않습니다.

명령행 실행(프로젝트가 Editor에 열려 있지 않을 때):

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe' `
  -batchmode -projectPath 'C:\work\Space_Dilemma' `
  -executeMethod SpaceDilemma.CustomInput.Editor.CustomInputValidation.RunBatch `
  -logFile "$env:TEMP\custom-input-validation.log"
```

이 검사는 Play 종료까지 기다린 뒤 스스로 exit code를 반환하므로 `-quit`를 붙이지 않습니다. 자동 검사 중에만 가상 Keyboard/Mouse 입력을 받도록 Editor 포커스 설정을 일시 조정하고 원래 값으로 복원합니다. 실제 게임의 포커스 정책을 변경하지 않습니다.

## 8. 제한과 실장치 확인

완료한 자동 검사/렌더/보존 감사의 최신 결과는 `Tests/Results/VALIDATION.md`에 기록합니다. 실물 HID 게임패드, Arduino 보드, USB 탈착, 보드 리셋, DTR/RTS 동작, 실제 Serial 지연, 펌웨어 디바운스는 별도 확인해야 합니다. 가상 Gamepad 이벤트는 Unity가 인식한 컨트롤러 이후의 Action/게임 경로를 검사하며 드라이버/무선 링크를 증명하지 않습니다.

Serial은 Windows 전용이며 독점 COM 열기를 사용합니다. 단일 플레이어 통합이므로 여러 로컬 플레이어를 자동으로 한 사용자에 합치지 않습니다. 원본 설정 생성 메뉴는 에셋이 이미 있으면 덮어쓰지 않습니다. 자동 설치를 끄려면 Settings.Install Automatically를 해제하고 원하는 GameObject에서 `InputHub.Initialize(settings, playerInput)`을 호출하세요.

참고: [Unity custom devices](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/manual/Devices.html), [Actions binding resolution](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/manual/ActionBindings.html), [PlayerInput + UI](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/manual/PlayerInput.html). 구현 API는 프로젝트에 설치된 Input System 1.20.1 소스와 대조했습니다.
