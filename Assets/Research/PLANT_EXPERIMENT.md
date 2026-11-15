# 식물 미션 첫 연구 안내

SampleScene의 `Space HUD / HUD Canvas / Plant Experiment Overlay`는 씬에 저장된 편집 가능한 UI다. 왼쪽에는 달의 부분중력(지구의 약 1/6)에서 뿌리 성장·방향을 비교하는 가상 연구 설명을 길게, 오른쪽에는 전력·시간·전력량과 게임용 차용 범위를 배치했다. 실행 중 UI를 만들지 않는다.

어느 식물 대상이든 첫 E키 상호작용을 **완료했을 때** 한 번 표시한다. 부분 충전이나 접근만으로는 열리지 않는다. 같은 플레이 세션의 두 번째·세 번째 식물에서는 다시 표시하지 않는다. 시작 설정에서 식물 미션이 이미 시작된 경우에도 뒤늦게 안내하지 않는다. 채취 시료는 기존처럼 각 시료의 설명을 표시한다.

공유 `SampleCollectionPopup` 제어기가 한 번에 한 설명창만 열고 기존 입력 컨텍스트와 시간 배율을 복원한다. 팝업 동안 게임 시계·피폭·시간 기반 연구 누적이 정지하며, 계속하기/Esc/Backspace로 닫으면 다시 흐른다. NASA 원문 버튼으로 브라우저를 열어도 설명창을 닫기 전까지 정지를 유지한다.

## 전력 패널에서 재사용할 데이터

`Assets/Research/Plant Experiment Information.asset` (`PlantExperimentInformation`)에 다음 기준값이 있다.

| 항목 | 값 | 구분 |
| --- | --- | --- |
| Reference Power Watts | 735 W | NASA 시설 설계값을 참고한 가상 달 재배장치 1세트 기준 |
| Observation Minutes | 1720분 = 28시간 40분 | GRAVI-1 첫 실험의 두 구간 시간을 더해 게임 관찰 시간으로 차용 |
| ObservationHours | 28⅔ h | 분 단위 설정에서 계산 |
| ReferenceEnergyKwh | 21.07 kWh | 735/1000 × 1720/60; 일정 전력으로 연속 가동할 때의 계산값 |

`ReferenceEnergyKwh`는 별도로 입력한 상수가 아니므로 전력·시간 값을 바꾸면 계산이 바뀐다. 20.58kWh는 735W × 28h 기준이다. 사용자의 28시간 40분 요청에 맞춰 팝업에서는 21.07kWh를 사용했다.

현 단계에서 이 기준값은 팝업이 사용한다. 기존 Scenario Rules의 전력 요청, 연구 누적률, 점수 계산은 바꾸지 않았다. 이후 패널에서 이 에셋을 참조해 실제 공급 W와 가동 시간에 따라 에너지를 적분하는 규칙을 연결할 수 있다. 장치 1세트의 수치이므로 식물 오브젝트 개수만으로 3배를 적용하지 않는다.

## NASA 출처와 차용 범위

1. B. Granath, **Kennedy Engineers Designing Plant Habitat For Space Station**, NASA, 2013-02-19. [원문](https://www.nasa.gov/science-research/kennedy-engineers-designing-plant-habitat-for-space-station/). 당시 설계 중인 시설이 약 735W를 사용한다고 설명한 값이다. 실제 달 중력의 뿌리 실험 소비전력을 측정한 자료가 아니다.
2. NASA/TP–2009–213146–REVISION A, **International Space Station Science Research Accomplishments During the Assembly Years: An Analysis of Results from 2000–2008**, 2009, 인쇄 p.94 / PDF 102쪽, GRAVI. [원문](https://ntrs.nasa.gov/api/citations/20090029998/downloads/20090029998.pdf#page=102). GRAVI-1 첫 실험의 15시간 성장 + 13시간 40분 원심가속 구간에서 시간만 더해 사용한다. 원래 실험은 달의 1/6g 실험이 아니므로 결과나 성공 조건을 달에 그대로 옮기지 않는다.

팝업의 달 연구 목적·대조군·기록할 항목은 가상의 연구 계획 설명이며 실제로 시뮬레이션한 뿌리 생장 결과가 아니다. 두 출처는 창의 인용문과 각각의 NASA 원문 버튼에서 확인할 수 있다. 게임 설명은 로컬 에셋을 사용하며 네트워크가 필요 없다.

검증: `Tools > Research > Validate Plant Introduction`. 결과는 `QA/PlantIntroduction/VALIDATION.md`를 따른다.
