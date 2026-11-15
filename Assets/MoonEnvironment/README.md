# Site6 / Nobile Rim 1 환경

2026-11-14 사용자 지정 프로젝트: `C:\work\Space_Dilemma`. 작업 씬은 기존 `Assets/Scenes/SampleScene.unity`다. 과거 `C:\work\NASA_Hackathon`의 New Scene/게임 구현과 구분한다.

## 현재 기본 모드: SPICE 지구와 고정 별 지도

`Moon Environment > Lunar Environment`의 **Use Celestial Ephemeris**는 `Data/Site6Celestial.json`을 사용한다. 편집 모드에서는 **CSV 시간 (시작 후 h)** 슬라이더로 태양·지구·별 천구를 조절한다. SampleScene의 Play 모드에서는 Survival Session의 공통 시간이 세 방향을 갱신한다.

지구의 방향·각지름·자전축·대륙 방향·밝은 면은 NASA/JPL SPICE 천체력으로 계산했다. 실제 지구 날씨나 당일 구름은 재현하지 않는다. 기존 Blue Marble 월별 지도는 2004년 11월 자료이고 지구는 구형으로 표현한다. 명암에는 지구에서 태양을 바라보는 별도 방향을 사용하며, 지면 Directional Light는 사용자 제공 SunAngle CSV를 계속 사용한다.

| UTC | 지구 방위각 | 지구 고도 | 각지름 | 밝은 면 |
| --- | --- | --- | --- | --- |
| 2024-03-21 15:33 | 325.688850° | 9.057445° | 1.807255° | 11.5127% |
| 2024-03-22 15:33 | 324.408079° | 8.055440° | 1.801828° | 6.1860% |
| 2024-03-23 15:33 | 323.081286° | 6.856030° | 1.799960° | 2.4374% |

고도는 관측점의 국지 수평면에 대한 값이다. 실제 능선·지형에 지구가 가려질 수 있다. 음수 고도라는 이유로 셰이더에서 원반을 강제 제거하거나, 지구가 보이게 고도를 올리지 않는다. 약 20km 평면 Terrain 밖의 달 곡면과 전역 지형 차폐는 계산하지 않는다.

별은 사용자 설정 `Illustrative Stars`로 표시한다. 씨드 20261114로 생성한 연출용 별 6000개다. 1024²×6면 R8 큐브맵의 Gaussian 별, 밉맵, Trilinear 필터링을 사용한다. 별 천구 전체는 달의 자세에 따라 회전하며 카메라가 이동해도 같은 방향에 보인다.

태양 `Sun Diameter=0.53`은 **각지름(도)** 이다. 광원 밝기·Bloom과 별개이며 원반 크기를 부풀려 밝기를 표현하지 않는다.

SampleScene의 `SampleSceneProfile`은 **Color Adjustments → Post Exposure +0.25 EV**를 적용한다. `LunarSky` 머티리얼의 **Sun Disk Radiance**는 태양 원반과 기존 HDR Bloom의 밝기를, **Sun Glare Intensity**는 태양 주변 빛번짐을 조절한다. **Earth Night-side Emission**은 어두운 면에 약한 푸른 발광을 더하는 연출값이다. 지구의 태양 조명, 위상과 원반 경계는 천체력과 각지름을 따른다.

## 별 디자인 조절

`Moon Environment > Lunar Environment > 별 디자인`에서 조절한다. 실제 별자리를 고려하지 않은 디자인 요소이므로 플레이 가독성에 맞춰 임의로 바꿔도 된다.

| 설정 | 범위/기본값 | 반영 방법 |
| --- | --- | --- |
| 별 표시 | 기본 ON | 즉시, Play 중에도 가능 |
| 밝기 | 0~4, 현재 0.85 | 즉시, Play 중에도 가능 |
| 색 | 기본 흰색 | 즉시, Play 중에도 가능 |
| 크기 배율 | 0.25~3, 기본 1 | 편집 모드에서 **별 디자인 적용 / 지도 갱신** |
| 별 개수 | 0~12000, 기본 6000 | 편집 모드에서 지도 갱신 |
| 배치 시드 | 기본 20261114 | 편집 모드에서 지도 갱신 |

크기·개수·시드에 미반영 변경이 있으면 Inspector에 경고가 표시된다. 크기는 밝기와 별도의 생성 설정이며 같은 시드와 별 개수에서는 중심 방향을 유지하고 Gaussian 폭을 바꾼다. 개수가 0이면 별이 사라진다. 개수를 줄이면 같은 시드의 앞부분 별을 남기며, 시드를 바꾸면 배치가 달라진다. 검증을 위한 기준 별 한 개는 시드와 무관하게 같은 방향이다. 작은 별은 텍셀 폭을 고려해 미리 필터링하고 밉맵/Trilinear를 유지한다. 매우 작거나 어두우면 약한 별은 보이지 않을 수 있다.

실행 중 크기를 바꾸기 위해 수백만 텍셀을 다시 생성하지 않는다. 지도를 한번 갱신하고 씬을 저장하면 게임은 생성된 큐브맵만 사용한다. `FixedIllustrativeStars.asset`은 모든 참조가 공유하므로 갱신하면 같은 지도를 쓰는 씬도 영향을 받는다. 이번 조절 기능 추가에서는 기존 별 지도와 밝기를 바꾸지 않았다.

## Tools 메뉴와 Editor 코드의 역할

| 종류 | 예시 | 완성 후 게임 실행에 필요한가? |
| --- | --- | --- |
| 검증/기록 | `Validate…`, `Capture Before` | 메뉴를 실행하지 않아도 된다. 수정 후 확인용 도구다. |
| 초기 설정/생성 | `Apply…`, `Build FULL SDEM` | 설정을 마친 씬에서는 다시 실행할 필요 없다. |
| 디자인 갱신 | `Rebake Illustrative Star Cubemap`, Inspector의 별 지도 갱신 | 크기·개수·시드를 바꿀 때 사용한다. 디자인이 정해지면 실행하지 않아도 된다. |
| Inspector 보조 | `LunarEnvironmentEditor.cs` | 편집 중 조절 UI·회전 고리·Undo·씬 저장/자료 재임포트 처리를 제공한다. 유지하는 것이 편리하다. |

이 코드들은 `Editor` 폴더에 있어 플레이어 빌드에는 포함되지 않는다. 실행하지 않는 것과 파일을 삭제하는 것은 구분한다. Inspector 코드는 별 생성기를 참조하고, 검증 코드끼리의 참조도 있으므로 아무 파일이나 하나만 지우면 Editor 컴파일 오류가 생길 수 있다. 코드 삭제 작업은 이번 요청에 포함하지 않았다.

게임 실행에는 `LunarEnvironment`, `LunarCelestialTable`, `SunAngleTable`, 하늘 셰이더/공유 매핑 파일, 씬의 머티리얼·텍스처·큐브맵·CSV/JSON이 필요하다. 검증 도구 없이도 이미 설정된 씬은 작동한다.

## 지구가 작게 보이는 이유와 크기 재검증

2024년 3월 기간의 지구 각지름은 약 **1.800~1.807°**다. 지구 적도 반지름 6378.137km와 달 관측점까지의 거리로 `2×asin(반지름/거리)`를 계산하고 Horizons의 시작·중간·종료 값과 대조했다. 최대 차이는 약 0.00025 arcsec이다. 기록은 `QA/Celestial/2024-03-21/horizons-comparison.json`에 있다.

카메라의 화면 중앙에서 지름은 다음 식을 따른다. 시야각과 각지름은 도 단위 입력을 라디안으로 바꿔 계산한다.

```
지름(px) = 화면 높이(px) × tan(지구 각지름/2) / tan(카메라 수직 FOV/2)
```

| 해상도 / 수직 FOV 60° | 화면 중심 기준 예측값 (2024년 3월 임무 시작) |
| --- | --- |
| 1280×720 | 19.67px |
| 1920×1080 | 29.50px |

Unity Game 뷰를 축소하면 원반도 같은 배율로 작아진다. 기존 수동 5.736°는 연출값이고 천체력 모드의 지름은 관측 거리로 계산한다. 이전 2026년 자료의 픽셀 측정은 `QA/StarDesign`에 있다.

## 천체력 자료와 좌표 처리

개발용 `MoonData/SunAngle/build_celestial_ephemeris.py`가 2024-03-21 15:33~23일 15:33 UTC를 5분 간격, 끝점 포함 **577개 기록**으로 저장한다. Unity는 저장된 JSON과 큐브맵을 읽는다. 방향과 자세는 구면/Quaternion 보간을 사용하며 범위 밖 시각은 끝점에 고정한다. Editor는 데이터 에셋을 다시 가져오면 캐시를 갱신한다.

관측점은 보존한 지형의 중앙 `(Unity X,Z)=(9997.5,9997.5)m`, 투영 좌표 `(84000,110000)m`다. 기존 남극 입체투영과 반지름 1737.4km로 위도 **−85.438100482746°**, 동경 **37.366669412769°**를 구했다. 원본 RAW의 중앙 높이 **5945.372875m**를 반영했다. 이는 원본 고도 기준이며 Unity의 Y에는 원본 최저 높이 860.002563m가 빠져 있다.

달 기준계는 `MOON_ME`(DE440의 `MOON_ME_DE440_ME421`), 지구 기준계는 `ITRF93`다. DEM의 지리 좌표를 이 달 기준계에 맞춰 사용하는 것은 명시적인 정합 가정이다. CSV의 정확한 추출점과 같다고 검증한 것이 아니다. 지형을 재배치/회전하거나 다른 관측점을 쓰면 새 데이터가 필요하다.

- NASA/JPL 커널은 `naif0012.tls`, `pck00011.tpc`, `de440s.bsp`, `moon_pa_de440_200625.bpc`, `moon_de440_250416.tf`, `earth_000101_270210_261114.bpc`다.
- 커널 URL·크기·SHA-256은 `QA/Celestial/2024-03-21/kernel-provenance.json`에 있다. [NAIF Generic Kernels](https://naif.jpl.nasa.gov/pub/naif/generic_kernels/)
- `spkcpo(..., LT+S)`로 지표 관측자의 겉보기 지구 방향을 구하고, 지구 자세는 광행시간을 뺀 시각에서 평가한다. 별 천구는 J2000 관성 방향을 달의 현재 프레임으로 옮긴다.
- Unity의 좌우 좌표계 차이는 SPICE xyz를 xzy로 바꾸는 고정 변환과 국지 동/상/북 기저에서 처리한다. 그 후 행렬식 +1인 회전만 Quaternion으로 저장한다. 지구 텍스처 모드에서는 Greenwich=+X, 동경 90°=+Z, 북극=+Y로 해석한다. 지구 지도는 경도(U)를 Repeat, 위도(V)를 Clamp로 샘플링해 반대 극의 색이 섞이지 않게 한다.
- 국지 방향에는 기존 남극 투영의 진북 yaw를 사용한다. DEM 중심에서 구한 SPICE 태양과 Moon Trek CSV 사이에 방위각 약 +2.38°, 고도 약 +0.24~+0.30°의 차이가 있다. 지면 광원은 첨부 CSV 각도를 사용한다. 표본별 차이는 `QA/Celestial/2024-03-21/sun-csv-residuals.json`에 있다.

재생성에는 NumPy와 개발용 `spiceypy==8.0.2`가 필요하다. 패키지는 `Temp/CelestialDeps`, 커널 캐시는 `Temp/CelestialKernels`에 둔다. 프로젝트 루트에서 `python MoonData/SunAngle/build_celestial_ephemeris.py --download --horizons`를 실행하면 새 기간의 JSON과 Horizons 대조 자료를 생성한다. 설치와 다른 CSV의 지정 방법은 `MoonData/SunAngle/README.md`를 따른다.

## 수동 지구 자유 회전

Hierarchy에서 **Moon Environment**를 선택하고 **Use Celestial Ephemeris를 끈다**. `Lunar Environment` Inspector 하단 **지구 자유 회전**의 원을 드래그한다. 천체력 모드에서는 이 수동값과 고리를 적용하지 않는다.

- **지구 표면**: 대륙과 자전축 방향을 자유롭게 돌린다. Scene의 회전 고리, X/Y/Z 로컬 축 버튼도 사용할 수 있다.
- **하늘 위치**: 지구가 보이는 하늘 방향을 회전한다. 구면의 극을 넘을 때도 Quaternion을 누적한다.
- `Ctrl+Z`로 되돌리고, **회전 초기화**로 선택한 회전 오프셋만 초기화한다.
- 기존 `Earth Azimuth`, `Earth Elevation`, `Earth Map Rotation`은 기준값이다. `EarthOrientation`과 `EarthPlacement`가 별도의 Quaternion 오프셋을 보관한다. 회전 중 Euler 각도로 변환하지 않는다.
- 코드에서는 `environment.RotateEarth(Vector3.up, degrees, Space.Self)` 또는 `EarthOrientation`, `EarthPlacement` 설정 후 `Apply()`를 사용한다. Play 모드에서는 매 프레임 적용된다.
- 지구의 조명·명암 경계는 태양의 월드 방향으로 계산하고, 대륙 표면 좌표에만 역회전을 적용한다.

수동 모드의 연출용 지구 각지름은 약 5.736°다. 천체력 모드의 지구 방향·자세·각지름·명암은 SPICE에서 계산하고 CSV에서는 계산 기간을 읽는다.

## Sun Angle CSV

사용자가 [NASA Moon Trek](https://trek.nasa.gov/moon/)에서 추출해 제공한 CSV를 `Data/SunAngle.csv`에 원문 그대로 복사했다. `Use Sun Angle Data`가 켜져 있으며, 기본 시간은 첫 기록이다.

| 항목 | 값 |
| --- | --- |
| 열 | `Time,Elevation,Azimuth` |
| 기록 수 | 51 |
| 시작 | 2024-081T15:33:00 = 2024-03-21 15:33:00 UTC |
| SPE 시간축 기준 | 2024-083T05:57:00 = 2024-03-23 05:57:00 UTC (38.4 게임시간, 실제 8분) |
| 끝 | 2024-083T15:33:00 = 2024-03-23 15:33:00 UTC |
| 간격 | 57분 36초, 총 48시간 |
| 첫 방위각 / 고도 | 2.976991° / +4.724044° |
| 끝 방위각 / 고도 | 338.580524° / +4.382764° |
| 고도 범위 | +4.382764°~+4.725455° |

2026년 원본은 `Data/Legacy`, 2022년 원본은 `Data/Legacy/2022-08-25`에 보관했다. 현재 씬은 기존 에셋 GUID로 새 태양과 천체력 파일을 참조한다. 방사선 브리핑과 시나리오의 선량 보정은 2022년 사건의 기존 데이터를 사용한다.

`CSV 시간 (시작 후 h)` 슬라이더로 시간을 바꾼다. UTC 시각과 실제 적용 중인 CSV 방위각·고도를 Inspector에서 확인할 수 있다. 두 기록 사이는 선형 보간하고 방위각은 0/360°를 가장 짧은 방향으로 연결한다. 범위 밖에서는 첫/끝 기록에 고정하며 임의 외삽·반복하지 않는다.

SampleScene의 Scenario Rules는 실제 1초당 0.08 게임시간을 진행해 48시간을 10분에 재생한다. Survival Session이 없는 씬에서는 `Animate Sun`과 `Sun Hours Per Second`를 사용한다. CSV가 없거나 손상되면 Inspector에서 오류를 표시하고 수동 태양 방향으로 돌아간다. 수동 값의 방위각은 월드 +Z=0°, +X=90°다.

CSV의 날짜 표기는 연도-연중일이다. UTC로 해석했으며, CSV 자체에는 시간대·관측 지점의 위경도·좌표계·현지 지형 차폐 여부가 들어 있지 않다. 사용자 제공 Site6 추출 자료로 취급하고 별도 천체력 계산 또는 정확한 추출점 검증을 수행한 것으로 설명하지 않는다.

## 남극 지도와 진북 보정

`InputData/manifest.json`은 중앙 경선 0°, 원점 남위 90°인 달 남극 입체투영 좌표를 기록한다. 변환기는 원본 지도 X 증가를 Unity +X, 지도 Y 증가를 Unity +Z로 보존했다. 남극에서는 지도 +Z가 모든 지점의 현지 진북과 같지 않다.

기존 20km 지형 중심의 투영 좌표 `(84000, 110000)m`를 기준으로 `True North Yaw = atan2(84000,110000) = 37.366673°`를 적용했다. 태양 월드 방향은 다음과 같다.

```
A = CSV Azimuth + True North Yaw
E = CSV Elevation
direction = (sin(A)*cos(E), sin(E), cos(A)*cos(E))
Directional Light.forward = -direction
```

남극 투영 축과 경선 관계는 [NGA의 UPS 설명](https://earth-info.nga.mil/?action=coordsys&dir=coordsys), [GDAL Polar Stereographic](https://gdal.org/en/stable/proj_list/polar_stereographic.html)에 부합한다. 지형 중심을 기준점으로 삼는 것은 현재 CSV에 정확한 추출점 좌표가 없기 때문에 선택한 **근사**다. 정확한 추출점의 투영 좌표를 알면 `True North Yaw`를 해당 지점 기준으로 갱신할 수 있다. 지형 루트를 회전했다면 그 월드 yaw도 반영해야 한다. 한 개 Directional Light로 전체 평면 지형에 같은 태양 방향을 적용하며, 20km 전역의 곡률·지점별 천체력을 계산하지 않는다.

## 표면과 조명

- 기존 [Poly Haven Moon 01](https://polyhaven.com/a/moon_01)의 디퓨즈·GL 노멀·러프니스·높이 맵을 사용한다. 자산은 CC0이며, [제작 설명](https://blog.polyhaven.com/moon/)에 나온 지상 촬영/모사 재료를 기반으로 한 PBR 자산이다. Site6 실측 표면 사진으로 설명하지 않는다.
- 원본 JPG/EXR/PNG는 변경하지 않았다. 원본 디퓨즈의 선형 휘도 `0.2126R+0.7152G+0.0722B`로 중립 회색 파생 알베도를 만들었다. 이 색 보정은 미술 처리이며 Site6 반사율 측정값이 아니다.
- 공용 Terrain Layer는 3.1m 반복, Normal Scale 0.8, Metallic 0이다. 실제 사용 알베도·노멀·마스크는 4096², 밉맵·Trilinear·Aniso16이다.
- URP 마스크는 **R=0(금속도), G=1(AO 미적용), B=원본 높이, A=1−선형 Roughness**로 패킹했다. 원본 러프니스가 매우 높아 매끈한 반사는 거의 없다. B는 저장만 하며 한 레이어에서 실제 정점 변위나 새 지형을 만들지 않는다.
- 16개 Terrain은 프로젝트 전용 URP Terrain/Lit 재질, Draw Instanced, 픽셀별 지형 노멀, Pixel Error 3, Basemap Distance 3000m를 사용한다. 원본 고도·알파맵·Terrain Layer 참조는 보존한다.
- 태양 색은 흰색, 태양 원반 0.53°, 작은 가독성 보조광 `Shadow Fill=0.025`를 사용한다. SampleScene의 태양은 URP 고품질 소프트 그림자를 사용한다. 별은 표면 노출과 독립적으로 항상 보인다.
- 현재 Scenario Rules의 `Sun Update Seconds=1`은 CSV 시각 4분 48초마다 태양 원반과 방향광을 함께 갱신한다. 지구와 별은 공통 시계를 연속으로 따른다. 값 0은 연속 이동이며 양수는 실제 플레이 초 기준 갱신 간격이다.
- PC URP 그림자 거리 12000m, 맵 4096, 캐스케이드 4개 분할 0.004/0.018/0.13을 사용한다. 가까운 구간은 약 48m까지 배정해 낮은 태양 아래 건물 그림자 경계 변화를 줄였다. 비교 렌더와 화면 밝기 변화 측정값은 `QA/MoonBase/ShadowStability`에 있다.
- 2022년 임무의 48시간 동안 태양은 지평선 아래다. 이 구간의 태양 Directional Light 강도는 0이며 지형과 기지는 기존 보조광과 자체 조명으로 보인다.

## 검증과 재실행

2022년 임무 기간의 Unity Play 검사 및 JPL Horizons 대조는 `QA/Celestial/2022-08-25/VALIDATION.md`에 있다. 검증 메뉴는 `Tools > Moon Environment > Validate Celestial Sky and Motion`, `Validate Celestial Play Mode`다. 기존 2026년 기간의 검사 기록은 `QA/Celestial/VALIDATION.md`를 따른다.

`Tools > Moon Environment > Validate and Render`는 Editor 검사와 1280×720/1920×1080 렌더를 실행한다. `Validate Play Mode`는 실제 Editor Play 모드에 들어가 CSV 재로드·회전·시간 진행·마지막 시각 고정을 검사한 뒤 편집 모드로 복귀한다. Play 모드 변경값은 저장하지 않는다.

앞선 표면 개선의 검증 기록은 프로젝트 루트 `QA/Environment`에 있다. 이 기록은 2026년 기간으로 실행한 Editor 검사 결과다.

원본 TerrainData 16개와 텍스처 4개는 SHA-256이 작업 전과 일치한다. 기존 Main Camera와 Terrain/root Transform도 보존했다. `Before`에는 작업 시작 시 씬·스크립트·설정 사본이 있다. 최초 before 렌더는 카메라 기본 aspect를 사용했고 최종 렌더는 명시적 16:9이므로 픽셀 단위 비교 자료로 사용하지 않는다.

`Apply Site6 Surface and Sun CSV`는 재질 재생성 및 이번 설정을 다시 적용하는 편집 메뉴다. 사용자가 추후 바꾼 환경·조명·재질 설정을 덮어쓸 수 있으므로 단순 검증에는 사용하지 않는다. 지형을 새로 생성하는 메뉴는 실행하지 않았다.
