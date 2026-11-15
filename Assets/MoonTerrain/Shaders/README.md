# 달 표면의 반복 무늬 완화

공용 Terrain 머티리얼에서 `Moon/Terrain/Regolith` 셰이더를 사용한다. 기존 Terrain Layer의 알베도·노멀·마스크와 3.1m 반복 크기를 읽는다. 월드 위치에 고정된 두 텍스처 오프셋을 부드럽게 혼합해 같은 돌이 격자로 반복되는 인상을 줄이며, 세 가지 크기의 밝기 변화를 함께 적용한다.

| 설정 | 기본값 | 동작 |
| --- | --- | --- |
| Macro contrast | 0.18 | 11·47·173m 크기의 중립 밝기 변화 |
| Detail offset strength | 0.85 | 일치하는 알베도·노멀·마스크의 샘플 위치 변화 |
| Detail blend size | 8m | 샘플 오프셋이 바뀌는 구역의 크기 |
| Detail fade start / end | 80 / 350m | 먼 거리의 세부 샘플을 원래 밉맵으로 복귀 |

노멀 세부 요철은 30~180m에서 점진적으로 줄인다. 월드 좌표에 고정된 무늬를 사용하며, 거리와 UV 미분에 따라 세부 표현을 전환한다. 별도의 지형 변위는 없다.

기존 URP 17.3.0 Terrain Lit 소스를 바탕으로 Forward·Deferred GBuffer·그림자·깊이·선택·인스턴싱·Terrain Holes 경로를 유지했다. 원거리 Base Pass도 같은 월드 밝기 변화를 적용한다. 원거리의 저해상도 Basemap은 기존 URP 생성기를 사용하며, 세부 오프셋은 Basemap 전환보다 먼저 소멸한다. GI 베이크용 Meta 경로는 기존 표면을 사용한다. 원본 DEM·RAW·표면 이미지·Terrain Layer를 그대로 참조한다.

이 표면 변화는 게임 미술 처리이며 Rim1의 실제 색상 지도나 관측 영상과 구분한다.

파생 파일의 기반 소스는 Unity URP 17.3.0의 `Shaders/Terrain`이다. Unity Technologies ApS 저작권 및 [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license)를 따른다. `RegolithVariation.hlsl`에는 프로젝트의 밝기·샘플 혼합 함수를 둔다. URP 버전을 바꾸면 기반 Terrain 패스의 인터페이스를 대조해야 한다.
