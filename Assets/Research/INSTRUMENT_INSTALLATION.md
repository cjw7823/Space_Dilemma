# 측정장비 설치 표시

SampleScene의 `Research Sites/Instrument Sites` 아래 세 ResearchTarget에 `InstrumentInstallation`을 연결했다.

- 설치 전: 면이 없는 청록색 반투명 정육면체 외곽선만 표시한다. 모서리 12개를 메시로 구성했고 충돌체는 없다.
- E키 충전 완료: 외곽선을 숨기고 `Installed Model` 아래 기존 모델을 표시한다. 연구 완료 이벤트와 기존 데이터 적립 연결을 유지한다.
- 팝업을 추가하거나 게임 시간을 멈추지 않는다. 시료 채취·첫 식물 상호작용의 기존 설명 팝업과 구분한다.
- 처음부터 설치 완료로 설정한 시나리오는 시작부터 모델을 표시한다.

표시 오브젝트와 메시·재질은 편집 가능한 씬/에셋으로 저장되어 있다. Play 중 모델이나 외곽선을 새로 생성하지 않는다. 현재 모델은 사용자가 각 사이트 아래에 배치한 InSight 모델을 그대로 사용한다. 모델의 실제 NASA 임무와 게임 속 지진계·열류량·자기장 측정기 이름을 동일한 장치라고 주장하지 않는다.

## 편집

사이트 부모 Transform을 옮겨 배치한다. Play 시작 시 지형이나 배치 지점 아래의 표면을 찾고, 모델 각 Renderer의 경계 바닥 및 외곽선 바닥의 모서리·중앙을 검사해 지면 접점을 맞춘다. 경사면에서 아래쪽 외곽선이 묻히지 않게 높이를 보정하며 정육면체는 수평을 유지한다. 모델이 숨겨져 있어도 메시 로컬 경계를 월드 좌표로 계산한다. 경계 상자를 이용한 정적 배치 근사이며, 다리별 지형 적응·착륙·현가장치 물리 계산은 아니다.

모델을 교체할 때는 `Installed Model` 아래에 넣는다. `InstrumentInstallation`의 Installed Model / Placement Outline 참조로 두 표시 대상을 바꿀 수 있다. 기존 부모의 단순 도형 Renderer는 비활성으로 보존했다. 원본 모델 재질·메시·모델 자식의 상대 배치는 유지한다.

`Assets/Research/InstrumentInstallation/Placement Outline.mat`에서 외곽선 색과 알파를, `Placement Outline` Transform에서 크기를 수정한다. 현재 정육면체 한 변은 약 3.045m이며 부모의 기존 비균일 스케일을 보정했다.

이미 씬에 저장되어 있으므로 일반 실행에 설정 메뉴를 실행할 필요는 없다. 재설정 메뉴는 `Tools > Research > Set Up Instrument Installation`, 검증 메뉴는 `Tools > Research > Validate Instrument Installation`이다. 재설정은 현재 자식 모델을 기준으로 정렬·초기 미설치 표시를 저장하므로 수작업 편집을 확인하고 사용한다. 검증 범위는 `QA/InstrumentInstallation/VALIDATION.md`를 따른다.
