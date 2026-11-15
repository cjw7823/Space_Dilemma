# Moon SEP Radiation Analyzer

현재 Unity SampleScene의 방사선 관측 기준은 **2022-08-27 11:12:00 UTC** 사건입니다. 전후 48시간의 관측 평균 **11.4664409722 → 65.1236631944 μGy/h**와 약 **5.6795배** 증가를 분석했습니다. SPE 전에는 전 평균에 `k=570`을 곱하고 1000으로 나눈 **6.5358713542 game mSv/게임시간**을 적용합니다. 48 게임시간을 실제 10분으로 압축하며, SPE는 플레이 8분(38.4 게임시간)에 시작합니다. [현재 게임 적용 안내](../../Assets/Survival/RADLAB_CALIBRATION.md)

Unity는 SPE 경계에서 누적량 `D`를 기록하고, 남은 9.6 게임시간 동안의 실외 선량률을 **`1.1 × (600 − D) / 9.6` game mSv/게임시간**으로 한 번 고정합니다. 현재 게임용 여유 배율 **1.1**에서 그 뒤 계속 실외·차폐 없음이면 **120/1.1 = 약 109.09초 후**, 플레이 **9분 49.09초**에 **600 game mSv**에 도달합니다. `ScenarioRadiation.speRemainingDoseMultiplier`는 Inspector에서 1 이상으로 조절하며, 일반식은 `max(1, 배율) × (600 − D) / 남은 게임시간`입니다. 배율 1은 임무 종료 시점에 정확히 도달합니다. 실내 대피와 능동차폐는 이 고정 선량률에 감소율을 적용하므로 최종 피폭량이 낮아질 수 있습니다. 실내외 이동에 따라 기준 선량률을 다시 계산하지 않습니다. 이 적응 보정은 플레이 세션의 누적량을 사용하므로 게임의 전후 비율이 관측 증가율과 달라질 수 있습니다.

하늘은 **2024-03-21 15:33~2024-03-23 15:33 UTC**의 Moon Trek·SPICE 자료를 사용합니다. 현재 방사선 모델의 관측 기간은 2022년이며, 두 자료는 공통 임무 시계에 연동됩니다. 아래 2024년 사건과 `calibrate` 결과는 별도로 비교한 후보 모델입니다. 기존 `output_unity_k570`은 관측 평균과 기본 변환 계수의 분석 기록이며, SPE 순간의 적응 보정은 Unity 실행 중에 계산합니다.

NASA **DONKI**의 태양 고에너지 입자(SEP) 이벤트 시각과 **RadLab**의 달 주변 방사선 관측값을 대조해, **각 이벤트가 발생한 실제 UTC 시각을 기준으로 전후 관측 구간**을 분석하는 Python 도구입니다.

기본 관측 분석 범위는 **SEP 전후 각각 48시간**입니다. `calibrate`는 선택한 SEP가 실제 플레이 8분에 오도록 48시간 임무의 시작과 종료 UTC를 정하고, 목표 피폭량에 맞춰 게임용 계수 `k`를 역산합니다. `scan`과 `analyze`의 24시간 배치 시계열은 기존 분석 예제에 해당합니다.

RadLab의 실측 흡수선량률(μGy/h)을 이용해 평상시 선량률·증가율·누적 흡수선량을 계산하고, 별도의 **게임 밸런싱 계수**를 적용한 피폭 게이지(mSv 표기)와 차폐 시나리오를 CSV로 출력합니다.

## 주요 기능

- DONKI API에서 SEP 이벤트를 조회하거나 사용자가 지정한 이벤트 시각을 분석
- SEP 이벤트별로 **실제 발생 시각 기준 전·후 각각 48시간(기본값)**의 RadLab 관측 구간 검사
- 이벤트별 데이터 충족률·기준 선량률·최대 선량률·상승 배율·누적 흡수선량 계산
- 관측 데이터 충족률이 기준 이상인 후보 중 **방사선 상승 배율이 가장 큰 이벤트 자동 선정** (`scan` 모드)
- `--refresh-radlab` 옵션으로 RadLab API의 관측값을 재조회하고 기존 CSV와의 대체 사용 여부 기록
- 실제 관측 변화 패턴을 바탕으로 1분 간격 시뮬레이션 CSV 생성
- `calibrate`로 현재 게임 시간에 맞춘 Moon Trek 입력 시각과 두 평균 방식의 게임용 `k` 산출
- 게임용 mSv 변환 및 SEP 증가분에 대한 가상 차폐 효과 비교

## 실행 환경

- Python **3.10 이상**
- 외부 Python 패키지 설치 불필요 (표준 라이브러리 사용)
- 입력용 RadLab CSV (기본 경로: `data/radlab_moon_observations.csv`)
- `scan`에서 `--donki-json`을 지정하지 않으면 DONKI API 접근을 위한 인터넷 연결 필요

아래 명령어는 `moon_sep_windows.py` 파일이 있는 디렉터리에서 실행하는 Windows PowerShell 예시입니다.

## 2024년 3월 23일 후보 임무

Moon Trek의 **Tools → Calculate Sun Angle**에서 현재 패널 배열 중앙 **남위 85.19366°, 동경 37.39638°**에 관측점을 놓습니다. 날짜와 시각은 모두 UTC입니다. [Moon Trek 입력란](https://trek.nasa.gov/moon/jpl/dijit/templates/SunAngleDialog.html?v=3.5.4-Moon)

| 항목 | 날짜 | 시:분:초 |
|---|---|---|
| Start Date (UTC) | 2024-03-21 | 15:33:00 |
| End Date (UTC) | 2024-03-23 | 15:33:00 |
| SEP 기준 시각 | 2024-03-23 | 05:57:00 |

SEP 시각은 SOHO COSTEP 관측이며 임무 시작 후 38시간 24분입니다. Moon Trek의 Interval을 50으로 두면 기존 내보내기와 같은 간격입니다. CSV의 첫 시각은 `2024-081T15:33:00`, 마지막은 `2024-083T15:33:00`입니다. [DONKI SEP 기록](https://ccmc.gsfc.nasa.gov/DONKI/view/SEP/29721/-1)

## 실행 모드 및 예시

### `calibrate` — 8분 SEP 임무와 게임용 계수 역산

```powershell
py .\moon_sep_windows.py calibrate `
    --sep-time "2024-03-23T05:57:00Z" `
    --sep-id "2024-03-23T05:57:00-SEP-001" `
    --sep-observer "SOHO: COSTEP 15.8-39.8 MeV" `
    --out ".\output_mission_2024_03_23"
```

이 명령은 지정한 시각 주변의 CRaTER-D1D2 관측을 RadLab에서 직접 조회합니다. 보관한 원본 CSV로 재현하려면 `--input <CSV 경로>`를 지정합니다. 입력한 SEP 시각과 관측 장비의 출처는 DONKI에서 확인합니다.

임무의 시작과 종료는 SEP 전 **38시간 24분**, 후 **9시간 36분**입니다. 두 선량률을 각 구간에서 일정한 값으로 적용하고, **시작 선량 0, 계속 실외, 차폐 없음**을 가정해 목표 시각에 도달하는 `k`를 구합니다. 기본 목표는 **플레이 600초의 600 game mSv**입니다. 종료 경계 전에 도달하도록 계산하려면 예를 들어 `--target-play-seconds 597.5`를 사용합니다.

| `mission_calibration.json` 모델 | 2024-03-23 전후 평균 (μGy/h) | 기본 목표의 k |
|---|---:|---:|
| `analysis_window_means` · SEP 전후 각각 48시간 평균 | 6.75810 → 3844.81419 | 16.142168468 |
| `mission_window_means` · 실제 48시간 임무 안에서 각 구간의 평균 | 7.12942 → 923.62283 | 65.641572766 |

선량률과 `k`는 같은 모델의 값을 함께 사용합니다. 후속 관측까지 포함하는 첫 번째 모델의 평균은 이번 사건에서 훨씬 높습니다. 관측이 비어 있거나 누적량이 0인 모델의 `k`는 `null`로 기록됩니다. 출력은 계산 결과이며 Unity 설정을 바로 변경하지 않습니다.

### `analyze` — 특정 SEP 이벤트 분석

```powershell
py .\moon_sep_windows.py analyze `
    --input ".\data\radlab_moon_observations.csv" `
    --sep-time "2022-08-27T11:12:00Z" `
    --pre-hours 48 `
    --post-hours 48 `
    --game-gain 200 `
    --shield-sep-fraction 0.10 `
    --out ".\output"
```

- `--sep-time`으로 지정한 **UTC 이벤트 시각**을 중심으로 `--pre-hours` 및 `--post-hours` 구간을 분석합니다.
- 기본적으로 로컬 RadLab CSV를 분석하며 `--refresh-radlab`으로 다시 조회할 수 있습니다. 이 모드는 입력한 SEP 시각을 기준으로 동작합니다.
- `--sep-id`, `--sep-observer`를 함께 지정하면 출력에 이벤트 ID와 관측 장비 설명을 기록할 수 있습니다.

### `scan` — DONKI에서 SEP 후보 자동 검색

```powershell
py .\moon_sep_windows.py scan `
    --input ".\data\radlab_moon_observations.csv" `
    --pre-hours 48 `
    --post-hours 48 `
    --min-coverage 0.98 `
    --out ".\output"
```

- 입력 CSV의 관측 기간 중 **전후 검색 범위를 확보할 수 있는 SEP 이벤트**를 DONKI API에서 조회합니다.
- 각 이벤트를 중심으로 관측 데이터 충족률을 계산하고, 기준을 만족하는 후보 중 최대 선량률/기준 선량률 비율이 가장 큰 후보를 선택하여 상세 CSV를 생성합니다.
- **CSV에 들어 있는 관측 기간이 곧 자동 검색 가능 기간의 한계**입니다. CSV의 시작·끝 근처에서 전후 48시간을 확보할 수 없는 SEP는 후보에서 제외됩니다.
- 기본적으로 방사선 관측값은 **입력 CSV에서 읽습니다.** DONKI 조회만으로 RadLab 관측값이 자동 갱신되는 것은 아닙니다.

**RadLab API에서 해당 이벤트의 관측자료도 다시 조회하려면:**

```powershell
py .\moon_sep_windows.py scan `
    --input ".\data\radlab_moon_observations.csv" `
    --refresh-radlab `
    --require-live-radlab `
    --out ".\output_live"
```

`--refresh-radlab` 조회가 실패하면 기본 동작은 입력 CSV 사용으로 복귀(fallback)하는 것입니다. `--require-live-radlab`을 함께 지정하면 재조회 실패 시 중단합니다.

**인터넷 없이 저장해 둔 DONKI JSON으로 검색하려면:**

```powershell
py .\moon_sep_windows.py scan `
    --input ".\data\radlab_moon_observations.csv" `
    --donki-json ".\data\donki_sep.json" `
    --out ".\output_offline"
```

### `check-apis` — NASA API 연결 및 응답 형태 확인

```powershell
py .\moon_sep_windows.py check-apis `
    --sep-time "2022-08-27T11:12:00Z" `
    --instrument "CRaTER-D1D2"
```

지정 시각 주변의 DONKI·RadLab API 요청 URL, 호출 결과 및 파싱된 레코드 수를 출력합니다. 어느 한 API 호출이나 파싱이 실패하면 종료 코드 `2`를 반환합니다.

## 명령행 인자

| 인자 | 기본값 | 설명 |
|---|---|---|
| 실행 모드 | 필수 | `scan`, `analyze`, `calibrate`, `check-apis` 중 하나 |
| `--input` | 기존 CSV (`scan`, `analyze`) / RadLab 직접 조회 (`calibrate`) | 입력 RadLab CSV 파일 경로 |
| `--out` | `output` | 분석 결과 저장 디렉터리 |
| `--instrument` | `CRaTER-D1D2` | 분석 대상 RadLab 장비 식별자 |
| `--sep-time` | 없음 | `analyze`, `calibrate`에서 필수인 ISO 8601 UTC 시각. `check-apis`에서도 선택 가능 |
| `--sep-id`, `--sep-observer` | 2022년 예제 사건에는 해당 ID와 SOHO 관측, 그 밖에는 `UNSPECIFIED` | 지정 시각의 DONKI 사건 ID와 관측 장비 기록 |
| `--donki-json` | 없음 | `scan`에서 네트워크 조회 대신 사용할 로컬 DONKI SEP API 응답 JSON |
| `--refresh-radlab` | 꺼짐 | 각 SEP 후보에 대해 RadLab API에서 선량률 데이터를 재조회. 실패 시 기본적으로 로컬 CSV로 복귀 |
| `--require-live-radlab` | 꺼짐 | `--refresh-radlab`과 함께 사용. RadLab 재조회 실패 시 CSV 대체 사용 없이 중단 |
| `--pre-hours` | `48` | SEP 발생 시각 **이전** 분석 시간(시간 단위, 양의 정수) |
| `--post-hours` | `48` | SEP 발생 시각 **이후** 분석 시간(시간 단위, 양의 정수) |
| `--min-coverage` | `0.98` | `scan`, `analyze`의 구간별 최소 관측 충족률. `calibrate`는 각 모델의 평균 구간에 전체 관측 필요 |
| `--game-gain` | `200.0` | `scan`, `analyze`의 고정 게임 계수 |
| `--target-game-dose` | `600` | `calibrate`의 목표 누적 game mSv |
| `--target-play-seconds` | `600` | `calibrate`의 목표 도달 시각. 플레이 시작 후 480~600초 |
| `--shield-sep-fraction` | `0.10` | 차폐 후 남기는 **기준 선량률 초과분(SEP 증가분)**의 비율 (`0~1`). `0.10`은 초과분 90% 감소를 뜻함 |
| `-h`, `--help` | — | 전체 CLI 도움말 표시 |

**시간 범위 옵션:** `--pre-hours`와 `--post-hours`는 분석용 관측 기간을 바꿉니다. `calibrate`의 임무 UTC 범위는 항상 38시간 24분 전부터 9시간 36분 후까지입니다. 기존 출력 파일명 `sep_anchored_96h_1min.csv`와 `game_48h_sep_at_24h_1min.csv`는 유지됩니다.

## 생성 파일

결과는 `--out` 디렉터리에 저장됩니다.

| 파일명 | 생성 조건 | 설명 |
|---|---|---|
| `all_sep_window_candidates.csv` | `scan`·`analyze` 실행 | SEP 후보 목록, 관측 충족률, 기준·최대 선량률, 증가 배율, 후보 적격 여부 |
| `sep_anchored_96h_1min.csv` | 적격 후보 존재 시 | **실제 SEP 시각 중심** 전후 관측자료를 1분 간격으로 전개한 CSV. 기본 설정에서 96시간 × 60분 = **5,760행** |
| `game_48h_sep_at_24h_1min.csv` | 적격 후보 존재 시 | 기존 분석 예제 시계열. SEP 전후 각각 24시간 × 분당 1행으로 총 2,880행 |
| `analysis_summary.json` | 적격 후보 존재 시 | 선택 이벤트의 통계, 전후 누적 흡수선량, 게임용 피폭 추정치 및 가상 차폐 결과 |
| `api_provenance.json` | `scan`·`analyze` 실행 | DONKI/RadLab API 요청 URL, 로컬 CSV 사용 여부, 재조회 실패 및 fallback 경고 |
| `mission_calibration.json` | `calibrate` 실행 | 임무·SEP UTC, 평균 선량률 산출 구간, 관측 충족률, 두 모델의 역산 k 및 RadLab 출처 |

> `check-apis` 모드는 파일을 생성하지 않습니다. 적격 후보가 없으면 1분 간격 CSV 및 `analysis_summary.json`은 생성되지 않습니다. 관측 누락이 있으면 해당 분의 계산값은 빈칸으로 남겨두며 임의로 측정값을 채우지 않습니다.

### CSV에서 주로 사용할 열

- `utc_minute_start`: 실제 UTC 시각
- `phase`: 실제 SEP 기준 전(`PRE_SEP`) / 후(`POST_SEP`)
- `raw_uGy_per_hour`: CRaTER 검출기의 시간당 **실측 흡수선량률**
- `game_unshielded_mSv_per_hour`: 게임용 배율 적용 후의 **차폐 없는** 시간당 피폭 상승량
- `game_unshielded_mSv_this_minute`: 게임용 1분 피폭 증가량
- `game_cumulative_unshielded_mSv`: 게임용 누적 피폭량 (차폐 없음)
- `game_sheltered_mSv_this_minute`: 가상 차폐 시나리오의 게임용 1분 피폭 증가량
- `game_cumulative_sheltered_mSv`: 가상 차폐 적용 시 누적 피폭량

**주의:** `shield_state`는 출력 CSV에서 SEP 이후 `ACTIVE`로 표시되는 **차폐 적용 가정 시나리오**를 나타냅니다. Unity에서 플레이어가 실제로 장치를 켰는지 여부는 게임 로직에서 별도로 처리해야 합니다.

## 게임용 피폭량 환산

Python의 `scan`·`analyze` 출력과 `calibrate`의 고정 계수 모델은 RadLab이 제공한 `absorbed_dose_rate`(μGy/h)에 게임용 계수를 적용합니다. Unity의 SPE 순간 적응 보정은 위에 설명한 누적량·남은 시간 계산을 사용합니다.

$$
\text{Game mSv/h} = \frac{\text{RadLab μGy/h} \times \text{gameGain}}{1000}
$$

- `gameGain`: 게임 밸런싱용 임의 배율 (기본값 `200`)
- `shieldSepFraction`: 기저 선량률보다 증가한 부분에서 차폐 후 남는 비율 (기본값 `0.10`)
- 누적 피폭량이 `600 game mSv` 이상이면 **임무 안전 기준 위반**으로 게임 종료 (Unity 게임 로직에서 구현)

기저 선량률(`baseline`)은 SEP 이전 분석 구간에 있는 시간별 흡수선량률 값의 **중앙값**을 사용합니다. 방사선량이 `baseline`을 초과하는 부분을 SEP 증가분으로 단순화한 뒤 가상 차폐를 적용합니다. 이는 실제 입자 종류별 기여도를 분해한 것이 아닙니다.

## 데이터 출처 및 주의사항

- [NASA DONKI](https://ccmc.gsfc.nasa.gov/tools/DONKI/) — SEP 이벤트 기록 및 관측 시각
- [NASA RadLab](https://visualization.osdr.nasa.gov/radlab/gui/data-api/) — 달 주변 방사선 관측 데이터

**본 도구는 NASA의 실제 관측 데이터에 기반하지만, 게임용 mSv는 우주비행사의 실제 인체 유효선량이 아닙니다.** CRaTER의 달 **궤도** 검출기 흡수선량률을 이용하며, 달 표면의 방사선 환경·우주복·기지 차폐 및 인체 조직별 피폭은 게임용 규칙으로 단순화되어 있습니다.

실측자료는 약 1시간 간격이며, 생성된 1분 간격 값은 각 시간 구간의 관측 선량률을 일정하다고 가정하여 배분한 **추정 시계열**입니다. 또한 DONKI의 SEP `eventTime`은 해당 관측 기록 시각이지, 방사선이 처음 증가한 정확한 순간이나 달에 입자가 도착한 순간을 보장하지 않습니다. SEP 기준 시각 이전에 방사선 증가가 나타날 수 있습니다.

**NASA-STD-3001 — 경력 누적 선량 600 mSv**

- 설명: 우주비행사의 **경력 누적 유효선량 한도**를 참고한 게임 종료 기준입니다. 단일 EVA 선량이나 즉각적인 건강 이상 임계값이 아니며, CRaTER 검출기 선량과 직접 비교할 수 없습니다.
- 출처: NASA, *Spaceflight Human-System Standard*, Volume 1, Sec. 4.8.2 (V1 4030). [NASA 공식 자료](https://www.nasa.gov/reference/4-0-human-performance/)
