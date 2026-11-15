# STEP 4 — 기지 전력

SampleScene에 저장된 오브젝트를 사용한다. 런타임에 UI를 생성하지 않는다.

## 사용

- Hierarchy `Base Power Control/Interior Power Console`: 파란색 실내 콘솔. 근처에서 E를 누르면 열린다. 인접 연구 장치와 E키를 공유하므로 더 가까운 대상이 우선한다.
- `Space HUD/HUD Canvas/Power Overlay/Power Window`: 편집 가능한 UI. 기본 비활성 상태이므로 편집할 때 Power Overlay를 켜서 확인하고, 실행 전 다시 끈다.
- 차폐 슬라이더: 출력 요청 0~100%. 실제 공급 출력과 W를 따로 표시한다.
- 연구시설 슬라이더: 시설별 전력 요청 W. 행 제목은 실제 소비 / 요청 W와 실제 ON 개수를 표시한다. 장치별 체크는 가동 요청, 녹색/빨강 표시는 실제 전원이다. OFF 또는 전력 부족 이유를 표시한다.
- Backspace 또는 닫기 버튼으로 복귀한다. ESC는 기존 일시정지 메뉴를 열며, 복귀하면 전력 UI가 복원된다. 전력 UI만 열었을 때는 게임 시간이 계속 흐른다.
- ON은 전원 공급 상태다. 시료 보관/장비 설치/재배 준비는 기존 E키 상호작용으로 별도 완료해야 연구가 누적된다.

## 조절

`Assets/Power/Base Power Rules - Test Values.asset`을 선택한다. Inspector의 Available Watts, Life Support Watts, Shield Maximum Watts 및 Samples/Instruments/Plants의 세 Devices 각각의 Watts/Priority를 편집한다. 시설 자체 Priority도 조절할 수 있다. 각 시설은 정확히 3개 정의를 유지한다. 설정은 실행 시작 시 복사되므로 다음 실행부터 적용된다. 플레이 중 UI 변경은 정의 에셋에 저장하지 않는다.

기본값은 총 1000W / 생명유지 250W / 차폐 최대 500W, 보관소 60·70·80W, 측정장비 40·50·60W, 재배 구획 70·80·90W다. **모두 게임 테스트용 임시 수치이며 NASA 장치 규격이 아니다.** 기본은 차폐 0%, 모든 연구 장치 가동 요청이다.

공급 순서는 생명유지 → 차폐 → 연구시설이다. 시설 및 장치 Priority는 작은 숫자부터, 동률이면 배열 순서부터 공급한다. 높은 우선순위의 요청 장치를 온전히 공급하지 못하면 그 시설의 후순위도 정지한다. 수동 OFF 장치는 건너뛴다. 시설의 실제 미사용분은 다음 시설에 사용할 수 있다. 분수 전력으로 연구 장치를 ON 처리하지 않는다.

총 전력이 필수 생명유지 요구량보다 작은 설정에서는 모든 가용 전력을 생명유지에 먼저 공급하고 부족 경고를 표시한다. 이 부족 자체의 사망 규칙은 아직 추가하지 않았다. 소비는 언제나 가용량 이하다.

## 코드 구조

- `PowerRules`: W 요구량과 우선순위 정의.
- `PowerBudget`: 씬과 UI에 독립적인 배분·합계·ON/OFF 계산.
- `BasePowerController`: 9개 ResearchDevice.SetPowered에 실제 전원을 적용. 전력 상태의 소유자다.
- `PowerPanel`: 실내 근접/시야 검사, E키, 입력 컨텍스트, 일시정지와 언어 전환, 씬 UI 갱신.
- `PowerFacilityRow`: 미리 배치한 슬라이더·토글과 시설 상태 연결.
- `Editor/PowerStepChecks`: 전력 계산 및 Play 모드 통합 회귀 검사. 기존 HUD 검사 메뉴에서 실행한다.

전력 OFF는 신규 연구 누적을 중단하고 기존 데이터를 보존한다. STEP 5에서는 실제 공급 전력에 따른 실내 SPE 차폐 효과와 시료의 유예시간 후 품질 저하를 연결했다. 사용 가능한 시료는 재공급 후 누적을 재개하며, 연구불가 품질까지 손상된 시료는 신규 누적이 재개되지 않는다. 전력 UI에는 실내 SPE 감소율과 시료별 품질을 표시한다. 자세한 임시 모델·설정·한계는 Assets/Survival/STEP5.md를 따른다. 태양광 발전량의 일조 연동·배터리·게임 저장은 아직 추가하지 않았다.

2026-11-15 사용자 확정으로 **SPE 딜레마 중의 포기는 장치별로 되돌릴 수 없다**. 수동 OFF·배분 감소로 전원을 잃거나 차폐 전력 때문에 공급이 끊긴 장치는 `PowerBudget.Abandoned`에 고정된다. 패널에서 `포기(영구)`로 표시하고 ON 요청을 막는다. 배분이나 차폐를 원복해도 그 장치의 데이터 누적은 재개하지 않으며, 같은 미션의 나머지 장치는 계속 가동한다. 일반적인 SPE 밖의 OFF/ON 동작은 복구 가능하다. 상세 규칙과 종료 설명은 `Assets/Survival/STEP6.md`, 검증은 `QA/EndingSacrifice`다.

## 검증

`Tools > Space HUD > Run Play Validation`. 현재 열린 씬을 검사하며 테스트는 실물 COM을 열지 않는다. 검사에서 STEP 3 장치 API를 직접 시험할 때만 전력 컨트롤러를 잠시 비활성화하고 원복한다. 실제 전력→연구 연결은 별도 STEP 4 검사에서 슬라이더·토글로 검증한다. 기록은 `QA/PowerStep4/VALIDATION.md`를 따른다.
