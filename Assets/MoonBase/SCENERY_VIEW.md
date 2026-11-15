# 경치 보기

SampleScene에서 **L** 또는 **게임패드 오른쪽 스틱 누르기(R3)**로 전환하고, 같은 버튼을 다시 누르면 기존 시점으로 돌아온다. 키보드·게임패드는 같은 `Player/SceneryView` 액션에 연결되어 있다. 물리 Serial 패널 버튼은 지정하지 않았다. 필요하면 기존 Actions Editor에서 이 액션에 추가한다.

관람 시점은 입장 당시 플레이어 또는 탑승 로버 위치에서 기본 80m 위에 고정된다. 머리 위에 더 높은 천장 Collider가 있으면 그 경계보다 5m 위로 올린다. 천장 검사는 한 번 수행하며, 플레이어·로버 자신과 TerrainCollider·Trigger는 제외한다. 전용 Cinemachine 카메라는 3인칭 충돌 회피를 사용하지 않아 천장 아래로 시점을 끌어내리지 않는다. 카메라 입장 1초·복귀 0.8초의 전환을 사용한다.

마우스 또는 오른쪽 스틱의 **좌우 입력만** 월드 Y축 회전에 사용한다. 상하 입력으로 높이·내려다보는 각도·롤을 바꾸지 않는다. 카메라 위치도 이동하지 않는다. 관람 중 이동·점프·달리기·승하차·연구·전력 콘솔 조작을 막는다. 로버는 탑승 상태를 유지한 채 멈추고 주행음을 중단하며, 복귀하면 기존 로버 카메라와 운전 조작으로 돌아온다. 도보의 시선·추적 대상·거리도 유지한다.

게임 시간과 세계의 물리 처리는 계속된다. Esc 일시정지나 다른 UI가 열리면 관람 회전도 멈추고, 닫으면 관람 모드로 돌아간다. UI 중에는 L/R3로 UI를 우회하지 않는다. 임무 종료 또는 컴포넌트 비활성화 시 관람 카메라와 관람용 입력 컨텍스트를 해제한다. 종료된 게임의 시간이나 종료 입력 차단은 풀지 않는다.

## 편집 위치

- 캐릭터의 `Moon Scenery View`에서 **Height**(상승 높이), **Pitch**(고정 내려다보는 각도), **Roof Clearance**를 수정한다.
- `Scenery Camera`는 씬에 저장된 CinemachineCamera이며 기본 비활성이다. Play 중 카메라나 UI를 새로 생성하지 않는다.
- `Assets/MoonBase/Scenery Camera Blends.asset`에서 이 카메라의 두 전환 시간을 바꾼다. 기존 Brain의 Default Blend는 유지했다.
- `Assets/CustomInputSystem/Profiles/SceneryView.asset`는 Look/SceneryView만 있는 전용 Scenery 액션 맵을 사용한다. 일반 UI보다 우선순위가 낮다. 기본 Look 바인딩과 감도는 기존 Player 맵과 같으며 이후 각 맵에서 편집할 수 있다. 기존 방향 입력의 중립 복귀 규칙을 사용하므로 모드 경계를 넘겨 누른 이동 입력은 손을 뗀 뒤 다시 조작한다.

검증 메뉴: `Tools > Moon Base > Validate Scenery View`. 범위와 결과는 `QA/SceneryView/VALIDATION.md`를 따른다. 초기 설치 이후 이 기능을 쓰기 위해 Setup이나 검증 메뉴를 실행할 필요는 없다.
