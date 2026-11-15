# 경치 보기

SampleScene에서 **L** 또는 **게임패드 오른쪽 스틱 누르기(R3)**로 전환하고, 같은 버튼을 다시 누르면 기존 시점으로 돌아온다. 진입은 `Player/SceneryView`, 복귀는 `Scenery/SceneryView`의 바인딩을 사용한다.

**USB Serial 패널의 원형 노브**는 `S,SEQ,K,S1,JX1,JY1,JX2,JY2,B` 패킷의 절대 위치 `K`를 사용한다. 10 미만에서 **10 이상**으로 올리면 관람에 진입한다. 이미 10 이상인 상태로 연결했다면 한 번 낮춰야 한다. **10~1000**은 최소·최대 높이 사이를 선형 보간한다. 최소는 진입 위치의 10m 위 또는 천장 상단에 여유를 더한 높이 중 큰 값이고, 최대는 같은 위치에서 L/R3로 진입할 때의 높이다. 10 미만으로 낮추거나 패널 연결이 끊기면 일반 시점으로 돌아온다. 노브로 진입한 상태에서도 L/R3로 닫을 수 있으며, 다시 노브로 진입하려면 10 미만으로 내렸다 올린다. L/R3로 진입하면 최대 높이에서 감상한다.

관람 시점은 입장 당시 플레이어 또는 탑승 로버 위치에서 기본 **1,000m 위**에 고정된다. 머리 위에 더 높은 천장 Collider가 있으면 그 경계보다 5m 위로 올린다. 기본은 기존 LunarEnvironment의 현재 지구 방향으로 진입하며, 지구 중심과 원반 가장자리 방향의 지형·건물 가림을 검사해 필요하면 더 올린다. 지구의 천체력 방향·크기·시간은 바꾸지 않는다. 천장 검사는 플레이어·로버 자신과 TerrainCollider·Trigger를 제외하며, 지구 시야 검사는 Terrain도 포함한다. 전용 Cinemachine 카메라는 3인칭 충돌 회피를 사용하지 않는다. 카메라 입장 1초·복귀 0.8초의 전환을 사용한다.

마우스 또는 오른쪽 스틱의 **좌우 입력만** 월드 Y축 회전에 사용한다. 수평 위치와 내려다보는 각도·롤은 고정한다. 관람 중 이동·점프·달리기·승하차·연구·전력 콘솔·입력 예제 메뉴 조작을 막는다. 로버는 탑승 상태를 유지한 채 멈추고 주행음을 중단하며, 복귀하면 기존 로버 카메라와 운전 조작으로 돌아온다. 도보의 시선·추적 대상·거리도 유지한다.

게임 시간과 세계의 물리 처리는 계속된다. Esc 일시정지나 다른 UI가 열리면 관람 회전과 높이 변경을 멈춘다. 노브로 관람 중이었다면 UI를 닫을 때 현재 노브 높이를 적용한다. 임무 종료 또는 컴포넌트 비활성화 시 관람 카메라와 관람용 입력 컨텍스트를 해제한다.

관람 중 `Space HUD/HUD Canvas/Gameplay HUD`를 비활성화해 미니맵·미션 보드·게이지·시계·버튼·SPE 경고를 모두 숨긴다. 다시 L/R3로 복귀하면 진입 전 HUD 표시 상태를 복원한다. Esc/설정/최종 결과 오버레이는 이 그룹 밖에 있어 필요한 메뉴와 종료 안내는 표시할 수 있다. 지구는 기존 지상 관측점의 천체력을 사용하는 하늘 표시이며 높은 관람 카메라를 위한 별도 천체력 재계산은 추가하지 않았다.

## 편집 위치

- 캐릭터의 `Moon Scenery View`에서 **Height**(상승 높이), **Pitch**(고정 내려다보는 각도), **Roof Clearance**를 수정한다.
- **Face Earth On Enter**를 끄면 현재 카메라의 수평 방향으로 진입한다. **Environment**, **Gameplay Hud**는 씬의 기존 환경과 숨길 HUD 그룹 참조다.
- `Scenery Camera`는 씬에 저장된 CinemachineCamera이며 기본 비활성이다. Play 중 카메라나 UI를 새로 생성하지 않는다.
- `Assets/MoonBase/Scenery Camera Blends.asset`에서 이 카메라의 두 전환 시간을 바꾼다. 기존 Brain의 Default Blend는 유지했다.
- `Assets/CustomInputSystem/Profiles/SceneryView.asset`는 Look/SceneryView가 있는 전용 Scenery 액션 맵을 사용한다. 일반 UI보다 우선순위가 낮다. 기본 Look 바인딩과 감도는 Player 맵과 같으며 각 맵에서 편집할 수 있다. 노브의 위치는 `MoonSceneryView`가 검증된 Serial 패킷에서 읽는다. 기존 방향 입력의 중립 복귀 규칙을 사용하므로 모드 경계를 넘겨 누른 이동 입력은 손을 뗀 뒤 다시 조작한다.

검증 메뉴: `Tools > Moon Base > Validate Scenery View`. 범위와 결과는 `QA/SceneryView/VALIDATION.md`를 따른다. 초기 설치 이후 이 기능을 쓰기 위해 Setup이나 검증 메뉴를 실행할 필요는 없다.

HUD 없는 지구 감상과 미션 미니맵 후속 검증은 `QA/SceneryMissionMap/VALIDATION.md`, 메뉴는 `Tools > Space HUD > Validate Scenery and Mission Map`을 따른다.
