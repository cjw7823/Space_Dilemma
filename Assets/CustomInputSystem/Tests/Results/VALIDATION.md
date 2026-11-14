# Custom Input System 검증 — 2026-11-15 (Asia/Seoul)

대상: `C:\work\Space_Dilemma`, Unity **6000.3.28f1**, Input System **1.20.1**, 실제 씬 `Assets/Scenes/SampleScene.unity`.

## 실행 결과

- Unity 배치 실행의 **실제 종료 코드 0**.
- Editor 검사 **49개 통과 / 0개 실패**: `editor-checks.txt`.
- 실제 SampleScene Editor Play 검사 **60개 통과 / 0개 실패**: `play-checks.txt`.
- 최종 Unity 로그에서 C# 컴파일 오류, 처리되지 않은 예외, UI action-type 경고가 없음을 확인했다.
- 실제 씬과 예제 UI를 **1280×720 / 1920×1080**으로 렌더했다. 두 PNG의 배치·잘림·슬라이더 영역을 확인했다.
- `git diff --check` 통과. 현재 변경된 기존 파일은 `Assets/MoonBase/MoonBasePlayer.cs` 하나이며 **23줄 추가 / 9줄 삭제**다. 나머지 신규 구현/설정/검증은 `Assets/CustomInputSystem/` 아래에 있다(폴더의 Unity `.meta`는 Assets 바로 아래에 자동 생성).

## 검사 범위

| 항목 | 확인한 증거 |
| --- | --- |
| 프로토콜 | 새 K,S1,JX1,JY1,JX2,JY2 순서, 필드 누락/초과, 잘못된 숫자/범위, 빈 행, CRLF, 분할/연속 수신, 96바이트 한계/폐기 후 복구, 비ASCII, SEQ 순환/불연속 |
| 보정 | 범위·중립점 변경, 음수 raw 범위, 데드존, 축 반전, 감도, 아날로그/버튼 비활성화, 버튼 비트 변경, 노브 delta 옵션 |
| 통신 | Windows COM 열거 API 실행. 실제 포트는 열지 않음. 가짜 `ISerialTransport`로 포트 점유 실패, 재시도, 정상 수신, 타임아웃 리셋, 재연결 후 수신 재개 검사 |
| 세 장치 | 가상 Keyboard/Mouse/Gamepad와 SerialController가 실제 PlayerInput 사용자에 동시에 페어링됨. 한 장치 해제 시 다른 장치의 Action이 계속 활성 |
| 기존 플레이어 | WASD/Shift/Space, 실제 CharacterController 이동·점프, 마우스 카메라 회전, Gamepad/Serial 카메라 회전, 로버 참조 유지 |
| 기존 상호작용 | Gamepad의 Interact로 기존 로버 탑승, 같은 프레임 Serial 누름/해제로 기존 하차 로직 실행 |
| 충돌 정책 | 강한 벡터 선택/비합산, 같은 강도에서 현재 소유자 유지, 중립 후 소유권 전환, 두 장치 버튼 OR, 절대값의 마지막 변경 source 소유권 |
| 짧은 버튼 | Serial press/up을 한 업데이트에 주입해 실제 PlayerInput의 Jump Performed/Canceled 둘 다 수신. 실제 EventSystem Submit/Cancel과 로버 상호작용도 검사 |
| 컨텍스트 | 우선순위/동률/토큰 Pop, Gameplay↔UI, Solar↔Oxygen, UI 중첩 후 이전 시설 복원, 기존 map 비활성, 스틱/버튼 잔류 방지와 중립 후 재입력 |
| UI | InputSystemUIInputModule과 PlayerInput이 같은 Action Asset 인스턴스를 공유. 탐색 초기 지연/반복 수(검사 구간 5회), 선택 계층 스크롤, 실제 Slider, Gamepad/Serial Submit으로 실제 uGUI Button 호출 |
| 시설 | 같은 Serial slider가 SolarGenerator/PowerOutput과 OxygenGenerator/OxygenOutput으로 바뀜. Gamepad trigger도 동일 경로 사용. ScriptableObject binding override로 knob를 같은 액션에 재연결 |
| 복원 | InputHub 종료 시 원래 PlayerInput Actions/control-scheme 동작 복원, 이후 원래 키보드 fallback 경로로 이동 가능 |

Editor Play 자동 검사 중에만 가상 Keyboard/Mouse를 Game 뷰로 라우팅하고 플레이어의 포커스를 설정했다. 검사 종료 시 Input System의 원래 Editor/Background 정책을 복원했다. 이것은 실제 사용자의 마우스·키보드 전체 수동 조작 검사와 구분한다.

현재 씬에서 Terrain은 **9개**다. 작업 시작 시 실제 파일/씬을 기준으로 한 값이며, 과거 지침의 16개로 되돌리거나 지형을 추가/삭제하지 않았다. Play에서도 같은 9개임을 검사했다.

## 원본 보존 감사

`preservation-audit.json`은 작업 전 SHA-256과 작업 후 파일을 대조한 결과다.

- 기존 **씬 6개**, **프리팹 206개**, **Input Actions 2개** 해시 일치.
- **WPF 파일 26개** 해시 일치. Arduino 펌웨어 파일은 수정하지 않았다.
- 기타 비교 대상까지 총 **243개 기존 파일**이 동일하고 누락 파일 0개다.
- 유일한 의도적 기존 소스 변경은 MoonBasePlayer다. PlayerInput 액션을 직접 읽던 곳에 선택적 InputHub 경로를 연결하고, UI 차단/비포인터 조작 시작/동일 Interact/시선 rate 처리를 넣었다. InputHub가 없으면 기존 경로를 사용한다. Starter Assets 원본 스크립트는 수정하지 않았다.

신규 파일 목록은 감사 JSON의 `new_implementation_files`에 있으며 Unity 자동 생성 `.meta`와 결과 PNG/TXT는 이 목록에서 제외했다.

## 구현 중 발견해 수정한 사항

- 배치 모드 Editor의 포커스 없는 가상 Keyboard/Mouse와 실제 플레이어 포커스 차이를 검사 도구에서 분리했다. 프로덕션 포커스 정책은 보존했다.
- Action Map 교체를 Hub Update로 모아 입력 callback 재진입을 피했다. 변경을 요청한 직후가 아니라 `ContextChanged`가 발생한 뒤 새 매핑이 적용된다.
- 비포인터 스크롤을 마우스 위치가 아닌 선택된 UI의 EventSystem으로 전달했다.
- 종료 시 생성한 UI 참조를 먼저 비우고 원래 PlayerInput을 복원해 InputActionReference 복원 예외를 제거했다.
- 높은 프레임 속도에서 스틱 delta를 미리 계산하면 기존 모터의 작은 값 무시 기준에 걸리는 문제를 수정했다. 스틱은 rate를 전달하고 모터 내부에서 시간 배율을 적용한다. 마우스 delta는 원래대로 유지한다.
- 실제 씬 이동 검사는 현재 위치의 콜라이더로 벽 없는 방향을 골라 수행한다. 검사 중 런타임 방향을 바꿨을 뿐 씬/배치 파일을 저장하지 않았다.

## 미검증과 실장치 체크

- 실물 Xbox/PlayStation/HID 드라이버 인식, Bluetooth/USB 차이, OS 장치 재연결.
- 실제 Arduino COM 열기/baud·parity·stop bits 일치, DTR/RTS 리셋, USB 탈착, 실제 수신 지연과 전송 주기.
- 보드의 실제 중립점/축 극성/원형 노브 방식/버튼 비트 의미와 디바운스.
- Windows standalone 실행 파일 빌드/실행, IL2CPP, 다른 OS의 COM 백엔드. 이번 검증은 Unity Editor 컴파일과 Editor Play다.
- 여러 로컬 PlayerInput의 자동 장치 배분은 범위 밖이며 현재 자동 설치는 단일 플레이어만 지원한다.

실장치에서는 Debugger에서 COM을 선택하고 새 형식의 S를 수신한 뒤, 가상 검사와 같은 Gameplay/UI/Facility 전환을 반복한다. 가상 Gamepad 검사는 Unity Gamepad 장치 이후의 경로를 검증하며 물리 HID 링크를 검증한 것은 아니다.
