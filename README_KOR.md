# Space Dilemma

[English README](README.md)

Space Dilemma는 달 기지에서 진행하는 짧은 시뮬레이션 게임입니다. 도보나 로버로 탐사하면서 달 시료를 채취·보관하고, 과학 장비를 설치하고, 식물 실험을 시작합니다. 태양 입자 사건으로 피폭량이 빠르게 쌓이기 시작하면 기지의 한정된 전력을 연구와 방사선 차폐에 어떻게 나눌지 결정해야 합니다.

## 주요 사용 데이터

### 방사선과 태양 고에너지 입자 사건

- **NASA CCMC DONKI**의 관측 시각을 기준으로 방사선 자료의 전후 구간을 나눴습니다. 현재 게임의 방사선 모델은 **2022년 8월 27일 11:12 UTC**에 기록된 태양 고에너지 입자 사건(SEP)을 사용합니다. 게임 속 48시간을 실제 플레이 10분으로 압축했으며, 사건은 게임 시간으로 38시간 24분, 실제 플레이로 8분에 시작합니다. [NASA CCMC DONKI](https://ccmc.gsfc.nasa.gov/tools/DONKI/)
- **NASA RadLab**은 달 정찰 궤도선(LRO)에 실린 CRaTER-D1D2 검출기의 흡수선량률 자료를 제공합니다. 관측값이 얼마나 갖춰졌는지 확인한 뒤 DONKI 기록 전후 각각 48시간의 평균을 구했습니다. Unity는 **사건 전 11.4664 μGy/h**, **사건 후 65.1237 μGy/h**를 실외 피폭 증가율의 기준으로 사용합니다. 두 평균에 게임용 계수 `k = 570`을 곱하고 1,000으로 나눠 게임 시간당 game mSv로 환산합니다. [NASA RadLab Data API](https://visualization.osdr.nasa.gov/radlab/gui/data-api/)

Python 분석 도구는 다른 사건 후보를 비교하고, 1분 간격의 추정 시계열을 내보내며, 목표 누적 피폭량에 맞는 게임용 계수를 계산할 수 있습니다. 기본 임무는 전후 평균 두 값을 사용하며, 종료 기준은 **600 game mSv**입니다. 기지 내부에서는 피폭량이 더 느리게 쌓입니다. [방사선 분석 도구 README](MoonData/Radiation_Analyzer/README.md) · [게임 적용 안내](Assets/Survival/RADLAB_CALIBRATION.md)

### 달 지형과 태양·지구의 위치

- **Nobile Rim 1 지형.** NASA의 5 m/pixel 고도 GeoTIFF를 Unity가 읽는 16비트 RAW 높이맵 타일로 변환하고, 타일의 지리적 범위와 고도 범위를 기록합니다. [NASA 고다드 달 고도 자료](https://pgda.gsfc.nasa.gov/products/104)
- **NASA Moon Trek 태양각.** 현재 씬은 2024년 3월 21~23일(UTC)의 태양 고도각과 방위각으로 지면의 햇빛 방향을 설정합니다. 하늘 계산과 2022년 방사선 관측은 서로 다른 기간의 자료를 사용하며, 게임에서는 같은 임무 시계에 따라 진행됩니다. [NASA Moon Trek](https://trek.nasa.gov/moon/) · [달 환경 안내](Assets/MoonEnvironment/README.md)
- **NASA/JPL SPICE와 Horizons.** 개발 도구는 고도 모델의 중심을 관측점으로 삼아 5분 간격으로 천체 배치를 계산합니다. Unity는 이 계산을 바탕으로 하늘에서 보이는 지구의 방향, 자세, 크기와 밝은 면을 표현합니다. 지구의 겉보기 크기는 Horizons 자료와 대조했습니다. [NASA/JPL SPICE](https://naif.jpl.nasa.gov/naif/) · [JPL Horizons](https://ssd.jpl.nasa.gov/horizons/)

Python 도구는 빌드 전에 데이터를 만들고 검증하는 데 사용합니다. 게임을 실행할 때는 빌드에 포함된 데이터를 읽습니다. [지형·천체 도구 README](MoonData/SunAngle/README.md)

## 실행 방법

1. Windows에서 `Build.zip`을 내려받고 압축을 모두 풉니다.
2. 압축을 푼 빌드 폴더에서 `Space_Dilemma.exe`를 실행합니다. 실행 파일은 함께 풀린 파일과 폴더 옆에 둡니다.
3. **게임 시작**을 선택하고 방사선 브리핑을 닫으면 임무가 시작됩니다.

## 조작법

### 키보드·마우스

게임 화면 안을 클릭하면 캐릭터를 조작할 수 있습니다.

| 동작 | 입력 |
| --- | --- |
| 이동 또는 로버 주행 | **WASD** 또는 **방향키** |
| 시선 회전 | **마우스** |
| 도보 달리기 또는 점프 | 각각 **왼쪽 Shift**, **Space** |
| 로버 탑승·하차 | **T** |
| 시료 채취·보관, 연구 장비 설치, 식물 실험 시작 | 대상 근처에서 **E 길게 누르기** |
| 기지 전력 제어 패널 열기 | 콘솔 근처에서 **E 누르기** |
| 경치 보기 진입·복귀 | **L** |
| 일시정지 또는 마우스 커서 해제 | **Esc**로 일시정지, **Tab**으로 커서 해제 |

### 커스텀 게임패드(USB 직렬)

커스텀 게임패드는 **USB 직렬(COM)**로 연결합니다. 이 장치의 USB HID 모드는 미구현입니다. 직렬 장치가 연결되지 않으면 기본 조작은 **키보드·마우스**입니다.

컨트롤러를 USB로 연결한 뒤 게임의 **옵션**에서 해당 COM 포트를 선택하거나 입력합니다. 장치에 맞는 전송 속도(기본 **115200**)를 설정하고 **연결 / 재연결**을 누릅니다.

| 동작 | 입력 |
| --- | --- |
| 이동, 로버 주행 또는 메뉴 탐색 | **조이스틱 1** |
| 시선 회전 또는 경치 보기 카메라 좌우 회전 | **조이스틱 2** |
| 로버 탑승·하차 또는 메뉴 선택 | **버튼 0** |
| 도보 점프 또는 지원되는 메뉴에서 뒤로 가기 | **버튼 1** |
| 입력 예제 메뉴 열기 | **버튼 2** |
| 도보 달리기 | **버튼 3** |
| 경치 보기 진입과 카메라 높이 조절 | **원형 노브**를 **10 미만**에서 **10 이상**으로 올립니다. **10~1000**으로 높이를 조절하고, **10 미만**으로 내리면 복귀합니다. |

커스텀 게임패드를 사용할 때도 연구·전력 콘솔 상호작용은 키보드 **E**, 일시정지는 **Esc**를 사용합니다.

## 기타 NASA 데이터 및 과학 참고 자료

NASA 공개 자료를 활용한 **지형·시료·미션 수치**와 게임 내 적용 방식을 정리합니다. 아래 수치는 별도 표기가 없는 한 **실제 달 기지에서 측정한 값이 아닙니다.**

### 1. 달 착륙 후보지와 지형

**Artemis III — Nobile Rim 1 착륙 후보지**
- 설명: NASA가 발표한 9개 유인 착륙 후보 지역 중 하나. **영구 기지 확정 부지는 아님.**
- 출처: [1] NASA, “NASA Provides Update on Artemis III Moon Landing Regions,” 2024. [Online]. Available: https://www.nasa.gov/news-release/nasa-provides-update-on-artemis-iii-moon-landing-regions

**PGDA Product 78 — Site06 / Nobile Rim 1**
- 설명: `Site06`의 지역명과 LRO/LOLA 기반 5 m/pixel 고도 자료의 근거.
- 출처: [2] NASA Goddard PGDA, “High-Resolution LOLA Topography for Lunar South Pole Sites.” [Online]. Available: https://pgda.gsfc.nasa.gov/products/78

**VIPER — Nobile 지역 탐사 배경**
- 설명: Nobile 지역의 과학적 탐사 가치를 설명. Artemis III 후보지 선정과는 **다른 임무**의 자료.
- 출처: [3] NASA, “NASA's Artemis Rover to Land Near Nobile Region of Moon's South Pole.” [Online]. Available: https://www.nasa.gov/news-release/nasas-artemis-rover-to-land-near-nobile-region-of-moons-south-pole

**PGDA Product 104 — 실제 달 표면 고도**
- 설명: 5 m/pixel SDEM을 Unity Terrain 높이맵으로 변환. 게임의 지형·조명·물리까지 NASA가 검증했다는 의미는 아님.
- 출처: [4] NASA Goddard PGDA, “Enhanced Topography Models for Selected Lunar South Pole Regions with Shape-from-Shading.” [Online]. Available: https://pgda.gsfc.nasa.gov/products/104

**NASA Moon Trek — 달 지형 시각화**
- 설명: 지형·위치·조명 환경의 시각적 참고. 특정 시각의 정확한 태양 고도각을 입증하는 자료는 아님.
- 출처: [5] NASA, “Moon Trek.” [Online]. Available: https://trek.nasa.gov/moon/#v=0.1&x=130770.45593261695&y=40368.27117919925&z=5&p=urn%3Aogc%3Adef%3Acrs%3AIAU2000%3A%3A30120&d=&locale=&b=moon&e=-95853.74084472646%2C-134081.5237426755%2C357394.65270996036%2C214818.066101074&sfz=&w=

### 2. 실제 달 암석 시료

**Apollo 15 — Sample 15016,0**
- 설명: Apollo 15 달 암석의 실제 시료·3D 모델 참고.
- 출처: [6] NASA Johnson Space Center, “Astromaterials 3D: Sample 15016,0.” [Online]. Available: https://ares.jsc.nasa.gov/astromaterials3d/sample-details.htm?sample=15016-0

**Apollo 16 — Sample 65035,0**
- 설명: Apollo 16 달 암석의 실제 시료·3D 모델 참고.
- 출처: [7] NASA Johnson Space Center, “Astromaterials 3D: Sample 65035,0.” [Online]. Available: https://ares.jsc.nasa.gov/astromaterials3d/sample-details.htm?sample=65035-0

**Apollo 17 — Sample 76535,0**
- 설명: Apollo 17 달 암석의 실제 시료·3D 모델 참고.
- 출처: [8] NASA Johnson Space Center, “Astromaterials 3D: Sample 76535,0.” [Online]. Available: https://ares.jsc.nasa.gov/astromaterials3d/sample-details.htm?sample=76535-0

> 세 시료는 **Nobile Rim 1에서 채취된 것이 아닙니다.**

### 3. 우주방사선 기준

**NASA-STD-3001 — 경력 누적 선량 600 mSv**
- 설명: 우주비행사의 **경력 누적 유효선량** 기준. 단일 EVA 선량이나 즉각적인 건강 이상 임계값이 아니며, 게임 센서 선량과 직접 비교할 수 없음.
- 출처: [9] NASA, “Spaceflight Human-System Standard, Volume 1, Sec. 4.8.2 (V1 4030).” [Online]. Available: https://www.nasa.gov/reference/4-0-human-performance/

### 4. 미션 수치와 근거

**Mission 01 — 식물 뿌리 방향 실험**
- 설명: **735 W**(ISS Plant Habitat 설계 참고), **28시간 40분**(ISS Gravi-1 실험 단계 합계)을 조합하여 **21.07 kWh**의 *게임용 에너지 예산* 산출. 실제 달 실험의 측정값이 아님.
- 출처: [10] B. Granath, “Kennedy engineers designing plant habitat for space station,” NASA, Feb. 19, 2013. [Online]. Available: https://www.nasa.gov/science-research/kennedy-engineers-designing-plant-habitat-for-space-station/
- 출처: [11] C. A. Evans *et al*., *International Space Station Science Research Accomplishments During the Assembly Years: An Analysis of Results from 2000–2008*, NASA/TP–2009–213146, Rev. A, “Threshold Acceleration for Gravisensing (Gravi),” p. 94, 2009. [Online]. Available: https://ntrs.nasa.gov/citations/20090029998

**Mission 02 — EVA 시료 극저온 보관**
- 설명: **395 W**는 FROSTE 검토 냉동기의 특정 운전점 인버터 입력값(80 K에서 순냉각 11 W). 보관시설 전체의 소비전력이나 모든 운전 조건의 값은 아님.
- 출처: [12] J. A. Sanford *et al*., “FROSTE: Frozen Return of Samples to Earth,” poster presented at the Jamboree and Poster Expo, Huntsville, AL, USA, May 16, 2024, NASA Technical Reports Server. [Online]. Available: https://ntrs.nasa.gov/citations/20240004448

**Mission 03 — EVA 과학장비 설치**
- 설명: **29 W / 52 kg**는 제안된 장비 6종(지진계·열류량 탐침·자력계·운석 실험·전자기 탐사 장비·중성자 검출기)의 전력과 질량 합계. **통신·제어·히터 등 부가 전력 제외.**
- 출처: [13] J. Edmunson and H. Haviland, “Science of the Moon,” Sec. 8, Ch. 46, Table 1.5, *Handbook of Lunar Base Design and Development*, 2020, NASA Technical Reports Server. [Online]. Available: https://ntrs.nasa.gov/citations/20205004514

### 5. 자료 사용 원칙

- NASA **공개 데이터**, 자료 기반 **계산값**, **게임 시뮬레이션**을 구분합니다. 임무 실패 조건·장비 고장·식물 생장 결과는 별도 검증 전까지 게임 규칙입니다.
- 모델과 텍스처의 라이선스는 [서드파티 고지](Assets/StreamingAssets/Space_Dilemma_THIRD_PARTY_NOTICES.md)를 참고하세요.
- 출처: [14] NASA, “NASA Media Usage Guidelines.” [Online]. Available: https://www.nasa.gov/nasa-brand-center/images-and-media/

*Space Dilemma는 NASA와 제휴하지 않았으며 NASA의 후원이나 승인을 받지 않았습니다.*
