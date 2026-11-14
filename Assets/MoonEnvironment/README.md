# Site6 / Nobile Rim 1 환경

2026-11-14 사용자 지정 프로젝트: `C:\work\Space_Dilemma`. 작업 씬은 기존 `Assets/Scenes/SampleScene.unity`다. 과거 `C:\work\NASA_Hackathon`의 New Scene/게임 구현과 구분한다.

## 현재 기본 모드: SPICE 지구와 고정 별 지도

`Moon Environment > Lunar Environment`에서 **Use Celestial Ephemeris**가 켜져 있다. `Celestial Ephemeris`에는 `Data/Site6Celestial.json`, `Star Cubemap`에는 `Data/FixedIllustrativeStars.asset`이 연결되어 있다. 기존 **CSV 시간 (시작 후 h)** 슬라이더 하나로 태양·지구·별 천구를 함께 움직인다. Play 모드의 `Animate Sun`도 이 공통 시간을 진행시킨다. 기존에 사용자가 선택한 시각은 설정 적용 시 초기화하지 않았다.

지구의 방향·각지름·자전축·대륙 방향·밝은 면은 NASA/JPL SPICE 천체력으로 계산했다. 실제 지구 날씨나 당일 구름은 재현하지 않는다. 기존 Blue Marble 월별 지도는 2004년 11월 자료이고 지구는 구형으로 표현한다. 명암에는 지구에서 태양을 바라보는 별도 방향을 사용하며, 지면 Directional Light는 사용자 제공 SunAngle CSV를 계속 사용한다.

| UTC | 지구 방위각 | 지구 고도 | 각지름 | 밝은 면 |
| --- | --- | --- | --- | --- |
| 2026-11-14 00:00 | 321.536022° | −1.080394° | 1.802130° | 80.7678% |
| 2026-11-15 00:00 | 320.093009° | −0.056086° | 1.806006° | 72.8896% |
| 2026-11-16 00:00 | 318.673407° | 1.113850° | 1.815209° | 64.1487% |

고도는 관측점의 국지 수평면에 대한 값이다. 실제 능선·지형에 지구가 가려질 수 있다. 음수 고도라는 이유로 셰이더에서 원반을 강제 제거하거나, 지구가 보이게 고도를 올리지 않는다. 약 20km 평면 Terrain 밖의 달 곡면과 전역 지형 차폐는 계산하지 않는다.

별은 사용자의 **항상 보이는 별** 선택에 따라 `Illustrative Stars=0.85`로 표시한다. 밝은 표면과 함께 이렇게 보이는 것은 노출을 단순화한 연출이다. 씨드 20261114로 생성한 연출용 별 6000개이며 실제 별자리 목록이 아니다. 1024²×6면 R8 큐브맵의 Gaussian 별, 밉맵, Trilinear 필터링을 사용하고 시간별 랜덤 생성·픽셀 hash·임의 반짝임을 사용하지 않는다. 별 천구 전체는 달의 자세에 따라 회전한다. 카메라 위치는 별 방향 계산에 사용하지 않아 이동 시 시차가 없다. 매우 좁은 FOV로 확대하면 별의 유한한 필터 폭과 큐브맵 해상도가 보일 수 있다.

태양 `Sun Diameter=0.53`은 **각지름(도)** 이다. 광원 밝기·Bloom과 별개이며 원반 크기를 부풀려 밝기를 표현하지 않는다.

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

2026-11-14 후속 재검증에서도 실제 각지름은 약 **1.802~1.815°**로 확인됐다. 지구 적도 반지름 6378.1366km와 관측점까지 거리 약 40.27~40.56만km로 `2×asin(반지름/거리)`를 계산하고, 새 Horizons 응답의 각지름과 비교했다. 최대 차이는 0.0004 arcsec 이하다. 태양 원반 0.53°의 지름 약 **3.4배**다.

카메라의 화면 중앙에서 지름은 다음 식을 따른다. 시야각과 각지름은 도 단위 입력을 라디안으로 바꿔 계산한다.

```
지름(px) = 화면 높이(px) × tan(지구 각지름/2) / tan(카메라 수직 FOV/2)
```

| 해상도 / 수직 FOV 60° | 예측값 (첫 시각) | 실제 Unity 렌더로 맞춘 원반 지름 |
| --- | --- | --- |
| 1280×720 | 19.61px | 약 19.54px |
| 1920×1080 | 29.42px | 약 29.36px |

FOV 30°·90°에서도 두 해상도로 측정했고 원형과 투영식이 일치했다. Unity Game 뷰가 1:1보다 축소 표시되면 화면에서 보이는 지름도 그 배율만큼 작아진다. 기존 수동 5.736°는 연출값이었고 현재 천체력 모드의 지름은 실제 거리 기준이다. 이번 재검증에서 지구를 확대하거나 카메라 FOV를 바꾸지 않았다. 원문 응답·독립 계산·픽셀 측정은 `QA/StarDesign`에 있다.

## 천체력 자료와 좌표 처리

개발용 `MoonData/Tools/build_celestial_ephemeris.py`가 2026-11-14 00:00~16일 00:00 UTC를 5분 간격, 끝점 포함 **577개 기록**으로 저장한다. 실행 파일에는 JSON·큐브맵만 들어가고 Python·SPICE 커널·네트워크 연결은 필요 없다. 방향과 자세는 구면/Quaternion 보간하고 범위 밖 시각은 끝점에 고정한다. 초기 파싱 후에는 TextAsset 문자열을 매 프레임 복사하지 않으며 Editor의 자료 재임포트 시 캐시를 무효화한다.

관측점은 보존한 지형의 중앙 `(Unity X,Z)=(9997.5,9997.5)m`, 투영 좌표 `(84000,110000)m`다. 기존 남극 입체투영과 반지름 1737.4km로 위도 **−85.438100482746°**, 동경 **37.366669412769°**를 구했다. 원본 RAW의 중앙 높이 **5945.372875m**를 반영했다. 이는 원본 고도 기준이며 Unity의 Y에는 원본 최저 높이 860.002563m가 빠져 있다.

달 기준계는 `MOON_ME`(DE440의 `MOON_ME_DE440_ME421`), 지구 기준계는 `ITRF93`다. DEM의 지리 좌표를 이 달 기준계에 맞춰 사용하는 것은 명시적인 정합 가정이다. CSV의 정확한 추출점과 같다고 검증한 것이 아니다. 지형을 재배치/회전하거나 다른 관측점을 쓰면 새 데이터가 필요하다.

- NASA/JPL 커널: `naif0012.tls`, `pck00011.tpc`, `de440s.bsp`, `moon_pa_de440_200625.bpc`, `moon_de440_250416.tf`, `earth_000101_270208_261113.bpc`.
- 커널은 [NAIF Generic Kernels](https://naif.jpl.nasa.gov/pub/naif/generic_kernels/)에서 확보했다. URL·크기·SHA-256은 `QA/Celestial/kernel-provenance.json`에 있다. 지구 자세 커널에는 예측 구간이 포함되며 과학적 오차가 0이라고 주장하지 않는다.
- `spkcpo(..., LT+S)`로 지표 관측자의 겉보기 지구 방향을 구하고, 지구 자세는 광행시간을 뺀 시각에서 평가한다. 별 천구는 J2000 관성 방향을 달의 현재 프레임으로 옮긴다.
- Unity의 좌우 좌표계 차이는 SPICE xyz를 xzy로 바꾸는 고정 변환과 국지 동/상/북 기저에서 처리한다. 그 후 행렬식 +1인 회전만 Quaternion으로 저장한다. 지구 텍스처 모드에서는 Greenwich=+X, 동경 90°=+Z, 북극=+Y로 해석한다. 지구 지도는 경도(U)를 Repeat, 위도(V)를 Clamp로 샘플링해 반대 극의 색이 섞이지 않게 한다.
- 국지 방향·투영 지도 yaw는 앞선 태양 처리와 동일한 기준이다. 최초 SPICE 태양 방향은 CSV보다 방위각 약 −0.793°, 고도 약 +0.063° 차이가 난다. 지점/모델 차이를 숨기지 않고 `QA/Celestial/sun-csv-residuals.json`에 모든 시각의 차이를 기록했으며 CSV를 수정하지 않았다.

재생성에는 NumPy와 개발용 `spiceypy==8.0.2`가 필요하다. 패키지 설치 위치는 `Temp/CelestialDeps`, 커널 캐시는 `Temp/CelestialKernels`로 프로젝트의 기존 Git 제외 영역이다. 프로젝트 루트에서 개발용 Python으로 `MoonData/Tools/build_celestial_ephemeris.py --download --horizons`를 실행하면 자료를 생성하고 Horizons 대조 자료도 저장한다. 이미 확보한 파일만으로 재생성할 때는 두 옵션을 생략한다. 별 지도의 재생성 메뉴는 `Tools > Moon Environment > Rebake Illustrative Star Cubemap`이다.

## 수동 지구 자유 회전

Hierarchy에서 **Moon Environment**를 선택하고 **Use Celestial Ephemeris를 끈다**. `Lunar Environment` Inspector 하단 **지구 자유 회전**의 원을 드래그한다. 천체력 모드에서는 이 수동값과 고리를 적용하지 않는다.

- **지구 표면**: 대륙과 자전축 방향을 자유롭게 돌린다. Scene의 회전 고리, X/Y/Z 로컬 축 버튼도 사용할 수 있다.
- **하늘 위치**: 지구가 보이는 하늘 방향을 회전한다. 구면의 극을 넘을 때도 Quaternion을 누적한다.
- `Ctrl+Z`로 되돌리고, **회전 초기화**로 선택한 회전 오프셋만 초기화한다.
- 기존 `Earth Azimuth`, `Earth Elevation`, `Earth Map Rotation`은 기준값이다. `EarthOrientation`과 `EarthPlacement`가 별도의 Quaternion 오프셋을 보관한다. 회전 중 Euler 각도로 변환하지 않는다.
- 코드에서는 `environment.RotateEarth(Vector3.up, degrees, Space.Self)` 또는 `EarthOrientation`, `EarthPlacement` 설정 후 `Apply()`를 사용한다. Play 모드에서는 매 프레임 적용된다.
- 지구의 조명·명암 경계는 태양의 월드 방향으로 계산하고, 대륙 표면 좌표에만 역회전을 적용한다.

사용자가 작업해 둔 수동 지구 위치·크기·밝기·회전값은 저장한 채 보존했다. 수동 크기 약 5.736°는 연출값이고, 천체력 모드의 실제 적용 크기는 위 표의 약 1.80~1.82°다. 지구 위치를 태양 CSV만으로 역산하지 않는다.

## Sun Angle CSV

사용자가 [NASA Moon Trek](https://trek.nasa.gov/moon/)에서 추출해 제공한 CSV를 `Data/SunAngle.csv`에 원문 그대로 복사했다. `Use Sun Angle Data`가 켜져 있으며, 기본 시간은 첫 기록이다.

| 항목 | 값 |
| --- | --- |
| 열 | `Time,Elevation,Azimuth` |
| 기록 수 | 51 |
| 시작 | 2026-318T00:00:00 = 2026-11-14 00:00:00 UTC |
| 끝 | 2026-320T00:00:00 = 2026-11-16 00:00:00 UTC |
| 간격 | 57분 36초, 총 48시간 |
| 첫 방위각 / 고도 | 90.170044° / 1.548597° |
| 끝 방위각 / 고도 | 65.863331° / 3.462417° |

`CSV 시간 (시작 후 h)` 슬라이더로 시간을 바꾼다. UTC 시각과 실제 적용 중인 CSV 방위각·고도를 Inspector에서 확인할 수 있다. 두 기록 사이는 선형 보간하고 방위각은 0/360°를 가장 짧은 방향으로 연결한다. 범위 밖에서는 첫/끝 기록에 고정하며 임의 외삽·반복하지 않는다.

`Animate Sun`을 켜면 Play 모드에서 진행한다. `Sun Hours Per Second`는 실제 1초당 기록 시간의 증가량이다. 기본 0.02는 72배 시간 압축이며 마지막 기록에서 멈춘다. 수동 편집한 시간도 평가 시 데이터 범위로 제한한다. CSV가 없거나 손상되면 Inspector에서 오류를 표시하고 수동 태양 각도로 돌아간다. 이 수동 값은 월드 +Z=0°, +X=90°다.

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
- SampleScene은 `Animate Sun`을 켜고 `Sun Update Seconds=1`로 설정했다. 태양 원반과 방향광은 실행 시간 1초 간격에 해당하는 CSV 시각으로 함께 갱신하며 지구는 공통 게임 시간을 계속 따른다. 값 0은 연속 이동이고 양수는 갱신 간격(초)이다. 현재 배속에서는 한 간격이 CSV의 72초다. 자동 재생을 끄면 시간 슬라이더의 시각을 바로 적용한다. 기초 상판의 자기 그림자 떨림은 해당 씬 인스턴스의 뒷면으로 그림자 맵을 생성해 줄였다.
- PC URP 그림자 거리 12000m, 맵 4096, 캐스케이드 4개 분할 0.004/0.018/0.13을 사용한다. 가까운 구간은 약 48m까지 배정해 낮은 태양 아래 건물 그림자 경계 변화를 줄였다. 비교 렌더와 화면 밝기 변화 측정값은 `QA/MoonBase/ShadowStability`에 있다.
- 최초 태양 고도가 약 1.55°라 기존 근접 검사 위치는 그늘에 들어간다. 밝게 보이도록 태양 고도를 임의로 올리지 않았다. 테스트 이미지는 그늘·햇빛 받는 능선·전체 지형을 각각 포함한다.

## 검증과 재실행

최신 검증 메뉴는 `Tools > Moon Environment > Validate Celestial Sky and Motion`, `Validate Celestial Play Mode`다. 천체력/화면/좌표/GPU 검사 **47개**, 실제 Editor Play 검사 **8개**, 기존 표면·수동 회전 회귀 검사 **34개**를 통과했다. 새 결과는 `QA/Celestial`, 기존 회귀 결과는 `QA/Environment`에 있다. 상세 수치와 재현 조건은 `QA/Celestial/VALIDATION.md`를 따른다. `Apply Offline Celestial Sky`는 새 모드·큐브맵을 연결하는 설정 메뉴이고 검증 메뉴와 구분한다.

`Tools > Moon Environment > Validate and Render`는 Editor 검사와 1280×720/1920×1080 렌더를 실행한다. `Validate Play Mode`는 실제 Editor Play 모드에 들어가 CSV 재로드·회전·시간 진행·마지막 시각 고정을 검사한 뒤 편집 모드로 복귀한다. Play 모드 변경값은 저장하지 않는다.

앞선 표면 개선의 검증 기록은 프로젝트 루트 `QA/Environment`에 있다. 그때의 Editor Play 검사 8개와 이번 천체력 Play 검사 8개는 별도 기록이다. 이번에 기존 수동 회전/표면 검사도 34개 재실행해 통과했다. 게임 빌드·실장치·사용자의 전체 키보드/마우스 플레이 테스트와 구분한다. Inspector 드래그/Scene 고리는 코드와 컴파일을 확인했으며 자동 동작 검사는 공개 회전 API 경로를 사용했다.

원본 TerrainData 16개와 텍스처 4개는 SHA-256이 작업 전과 일치한다. 기존 Main Camera와 Terrain/root Transform도 보존했다. `Before`에는 작업 시작 시 씬·스크립트·설정 사본이 있다. 최초 before 렌더는 카메라 기본 aspect를 사용했고 최종 렌더는 명시적 16:9이므로 픽셀 단위 비교 자료로 사용하지 않는다.

`Apply Site6 Surface and Sun CSV`는 재질 재생성 및 이번 설정을 다시 적용하는 편집 메뉴다. 사용자가 추후 바꾼 환경·조명·재질 설정을 덮어쓸 수 있으므로 단순 검증에는 사용하지 않는다. 지형을 새로 생성하는 메뉴는 실행하지 않았다.
