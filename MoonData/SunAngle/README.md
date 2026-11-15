# Space Dilemma — Lunar Terrain & Celestial Tools

NASA/JPL의 달 지형 및 천체 궤도 데이터를 Unity 프로젝트에서 활용하기 위한 **개발용 Python 도구 모음**입니다. 실제 달 고도 데이터를 Terrain으로 변환하고, 특정 달 표면 위치에서 보이는 지구·태양·별의 방향과 지구의 겉보기 크기를 계산·검증합니다.

> 이 스크립트들은 Unity **빌드 전 데이터 생성·검증용**입니다. 게임 실행 중 Python이나 NASA API를 호출하지 않도록 결과 RAW/JSON 파일을 미리 생성합니다.

## 도구 목록

| 스크립트 | 역할 | 주요 산출물 |
|---|---|---|
| `convert_full_sdem.py` | 달 DEM GeoTIFF 전체 영역을 Unity Terrain 높이맵용 16비트 RAW 타일로 변환 | `moon_rXX_cXX.raw`, `manifest.json` |
| `build_celestial_ephemeris.py` | NASA/JPL SPICE로 달 표면 관측 지점에서의 지구·태양 방향, 지구 회전, 별 배경 방향, 지구 시직경 등을 5분 간격 계산 | `Site6Celestial.json`, 천체 계산 검증 자료 |
| `verify_earth_apparent_size.py` | 생성된 지구 시직경을 JPL Horizons 값과 비교하고 Unity 카메라 화면에서의 예상 지구 크기를 계산 | `earth-size-independent.json` |

## 실행 환경 및 파일 배치

Python과 아래 패키지가 필요합니다.

```powershell
py -m pip install numpy rasterio scipy spiceypy==8.0.2
```

`build_celestial_ephemeris.py`와 `verify_earth_apparent_size.py`는 스크립트 위치에서 **두 단계 위의 디렉터리**를 Unity 프로젝트 루트로 인식합니다(`Path(__file__).resolve().parents[2]`). 따라서 다음과 같이 배치해야 경로가 맞습니다.

```text
Space_Dilemma/
├─ Tools/
│  ├─ convert_full_sdem.py
│  └─ Celestial/
│     ├─ build_celestial_ephemeris.py
│     └─ verify_earth_apparent_size.py
├─ Assets/
│  ├─ MoonTerrain/InputData/        # 지형 변환 결과
│  └─ MoonEnvironment/Data/
│     └─ SunAngle.csv              # 천체 계산 기간 및 태양각 비교용 입력
├─ Temp/                            # SPICE 커널 다운로드/개발 의존성
└─ QA/                              # 검증 결과
```

> 기존 프로젝트에서 스크립트가 다른 디렉터리에 있다면 위 두 파일의 `ROOT` 계산식을 실제 프로젝트 구조에 맞게 수정해야 합니다.

## 1. 달 지형 변환 — `convert_full_sdem.py`

**역할:** 달 고도 GeoTIFF의 전체 범위를 유지하면서 Unity Terrain에서 사용할 수 있는 높이맵 타일로 변환합니다. Unity가 지원하는 `(2^n + 1)` 해상도에 맞춰 높이를 보간하며, 원본의 지리 좌표·지도 범위·고도 범위를 메타데이터로 저장합니다.

### 실행

기존 프로젝트에서 사용한 PowerShell 명령:

```powershell
python .\Tools\convert_full_sdem.py `
    --input "C:\work\Space_Dilemma\MoonData\A307_GLDELEV_005.tif" `
    --output "C:\work\Space_Dilemma\Assets\MoonTerrain\InputData" `
    --resolution 1025
```

실제 변환 전 예상 타일 수와 지도 크기만 확인하려면 `--dry-run`을 추가합니다.

### 생성 파일

- `moon_r00_c00.raw` 등: Unity Terrain용 **16비트 little-endian** 높이맵 타일
- `manifest.json`: 타일 개수·해상도·실제 지도 크기(m)·고도 범위·원본 CRS·지리 좌표·NoData 처리 내역

**주의:** 재격자화(보간)는 새로운 NASA 실측 고도 데이터를 생성하는 것이 아닙니다. NoData가 있다면 기본적으로 오류로 중단하며, `--nodata nearest`를 명시한 경우에만 주변 값으로 대체합니다. 기존 결과 덮어쓰기에는 `--force`가 필요합니다.

## 2. 천체 위치 데이터 생성 — `build_celestial_ephemeris.py`

**역할:** 지형 `manifest.json`과 RAW 고도맵에서 관측 지점을 산출하고, **NASA/JPL SPICE** 커널을 이용해 해당 달 표면 위치에서 보이는 지구·태양·별 배경의 방향과 회전을 계산합니다. 기존 `SunAngle.csv`의 시작·종료 UTC를 기준으로 **5분(300초) 간격** 데이터를 생성합니다.

### 필요한 입력

- `Assets/MoonTerrain/InputData/manifest.json`
- `Assets/MoonTerrain/InputData/moon_r01_c01.raw`
- `Assets/MoonEnvironment/Data/SunAngle.csv`
- SPICE 커널 파일들(`Temp/CelestialKernels/`, 첫 실행 시 `--download`로 획득)

### 실행

```powershell
# 프로젝트 루트에서 실행
python -m pip install --target Temp/CelestialDeps numpy spiceypy==8.0.2
python .\MoonData\SunAngle\build_celestial_ephemeris.py --download --horizons

# 커널을 이미 내려받았다면 로컬 데이터로 재생성
python .\MoonData\SunAngle\build_celestial_ephemeris.py
```

별도로 받은 CSV를 먼저 검증하려면 `--csv <파일> --output <JSON 경로> --qa-dir <검증 폴더>`를 사용합니다. 기본 입력과 출력은 Unity가 참조하는 `Data/SunAngle.csv`와 `Data/Site6Celestial.json`입니다.

### 생성 파일

- `Assets/MoonEnvironment/Data/Site6Celestial.json`: UTC 시작 시각, 총 기간, 관측 지점, 시간별 지구·태양 방향, 지구/별 회전 쿼터니언, 지구 시직경, 조명 비율 등의 샘플
- `QA/Celestial/<시작 UTC 날짜>/kernel-provenance.json`: 사용한 SPICE 커널의 출처·SHA-256
- 같은 폴더의 `site.json`: 관측 지점 좌표·고도
- `sun-csv-residuals.json`: `SunAngle.csv`와 SPICE 계산 태양각의 차이
- `interpolation-errors.json`, `offline-checks.json`: 보간·회전 검증 결과
- `--horizons` 사용 시 `horizons-query.txt`, `horizons-response.json`, `horizons-comparison.json`: JPL Horizons 교차 검증 자료

**주의:** 관측 지점은 DEM 중심으로 가정합니다. 스크립트 내에도 원본 CSV의 정확한 좌표와 DEM 좌표계의 SPICE 달 기준 프레임 정합 여부가 확정되지 않았다는 주석이 있으므로, 천체 배치의 절대 정확도가 중요하다면 해당 가정을 검증해야 합니다. `--download` 및 `--horizons`에는 인터넷 연결이 필요합니다.

## 3. 지구 겉보기 크기 검증 — `verify_earth_apparent_size.py`

**역할:** 생성된 `Site6Celestial.json`의 지구 거리에서 계산한 **지구의 시직경(각도)**을 JPL Horizons의 시직경과 대조합니다. 또한 세로 FOV 60° 카메라를 가정해 720p·1080p 화면에서 지구가 차지할 예상 픽셀 지름을 계산합니다.

### 실행

```powershell
# 생성기가 저장한 같은 기간의 Horizons 응답으로 검증
python .\MoonData\SunAngle\verify_earth_apparent_size.py

# 시직경 자료를 Horizons에 다시 요청해 검증
python .\MoonData\SunAngle\verify_earth_apparent_size.py --horizons
```

### 생성 파일

- `QA/Celestial/<시작 UTC 날짜>/earth-size-independent.json`: 비교 시각별 지구 거리, 계산/검증 시직경, 오차, 예상 화면 픽셀 크기, 태양 대비 크기
- `--horizons` 사용 시 `earth-size-horizons.json`, `earth-size-query.txt`: JPL Horizons 원본 응답 및 요청 URL

이 검사는 생성기의 `--horizons` 옵션으로 저장한 요청 또는 응답을 사용합니다. 세 시각이 현재 천체력의 시작·중간·종료와 일치하는지 확인합니다.

## 권장 작업 순서

1. `convert_full_sdem.py`로 RAW 타일과 `manifest.json`을 생성합니다.
2. 게임에서 사용할 시간 범위에 해당하는 `SunAngle.csv`를 준비합니다.
3. `build_celestial_ephemeris.py --download --horizons`로 천체 시계열을 생성하고 검증합니다.
4. `verify_earth_apparent_size.py --horizons`로 지구의 겉보기 크기를 검증합니다.
5. 생성한 `Site6Celestial.json`과 지형 입력 파일을 Unity 프로젝트에서 사용합니다.

**데이터 출처:** [NASA/JPL NAIF SPICE 커널](https://naif.jpl.nasa.gov/naif/) · [JPL Horizons](https://ssd.jpl.nasa.gov/horizons/) · 달 지형 GeoTIFF는 사용자가 별도로 제공하는 DEM입니다.
