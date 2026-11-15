# Astronaut 플레이어 외형

SampleScene의 기존 PlayerArmature에 사용자 제공 `Assets/Model/Astronaut.glb` 외형을 적용했다. 원본 GLB는 수정하지 않고 `Assets/MoonBase/AstronautPlayer`에 파생 에셋을 만들었다.

- `Astronaut Skinned Mesh.asset`: 원본 19,143개 정점·UV·삼각형·재질 슬롯 2개를 유지하고, 높이 1.8m에 맞춘 스킨 메시. 원본의 약간 내려간 팔을 바인딩용 T 자세로 보정했다.
- `Astronaut Humanoid Avatar.asset`: 체형에 맞춘 22개 Humanoid 관절과 Avatar.
- `Astronaut Visual.prefab`: 뼈대와 SkinnedMeshRenderer를 담은 편집 가능한 외형 프리팹. 원본 모델의 두 재질과 텍스처를 참조한다.

리깅과 가중치는 원본의 좌표·형태를 확인해 모델 전용 규칙으로 생성했다. 관절 주변은 최대 4개 관절에 가중치를 나누고 헬멧·바이저는 머리, 등짐은 가슴에 고정한다. 등짐은 허리 아래와 목 위까지 이어지는 구조여서 다리·머리 가중치에서 분리했다. 손가락별 관절·표정 리깅은 추가하지 않았다. 원본 파일 자체에 애니메이션이 생긴 것은 아니며 파생 프리팹이 기존 Starter Assets Humanoid 애니메이션을 사용한다.

기존 Animator의 Controller와 이동·충돌체·달 중력·점프·카메라·입력·상호작용 스크립트를 유지하고 Avatar와 표시 메시를 연결했다. 이전 캐릭터 메시와 뼈대도 씬에 보존하며 이전 Renderer만 끈 상태다. 로버 탑승 시 기존 로직이 새 외형도 숨기고 Animator를 정지하며, 하차 시 복원한다. 모델과 리그는 실행 전에 씬/프리팹에 저장되어 있고 실행 중 생성하지 않는다.

## 편집 및 재생성

`PlayerArmature/Astronaut Visual`에서 표시 메시·재질·뼈대를 확인할 수 있다. 머리·등짐 크기는 선택한 원본 모델의 형태를 따른다. 원본 GLB의 크기나 형태를 교체하면 현재 좌표 기반 가중치 규칙도 다시 맞춰야 한다.

`Tools > Moon Base > Build Astronaut Player Visual`은 파생 메시·Avatar·프리팹을 다시 생성한다. `Apply Astronaut Player Visual`은 현재 SampleScene의 기존 플레이어에 연결한다. 둘 다 편집 중 실행하는 설정 도구이며 Play 전에 실행할 필요는 없다. 생성물을 수작업으로 수정한 뒤 Build를 실행하면 파생 에셋을 덮어쓰므로 편집 내용을 확인한다.

검증 메뉴는 `Tools > Moon Base > Validate Astronaut Player`. 실제 수행 범위와 화면은 `QA/AstronautPlayer/VALIDATION.md`를 따른다.
