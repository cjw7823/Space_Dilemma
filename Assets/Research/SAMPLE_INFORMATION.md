# 달 시료 설명 팝업

SampleScene의 `Space HUD / HUD Canvas / Sample Information Overlay`는 씬에 저장된 UI다. 편집하려면 Overlay를 임시 활성화하고, 편집 후 비활성화한다. 실행 중 UI를 생성하지 않는다.

- Lunar Sample 1 → 아폴로 15호, 15016-0: 저티타늄 감람석 현무암, 해들리 릴 관측 지점 2–3 사이.
- Lunar Sample 2 → 아폴로 16호, 65035-0: 유리질로 덮인 두 주요 암석 성분의 각력암, 데카르트 고지 관측 지점 5.
- Lunar Sample 3 → 아폴로 17호, 76535-0: 트록톨라이트, 타우루스–리트로 계곡 북쪽 산지 관측 지점 6.

E키 채취 완료 이벤트에서 해당 시료 설명을 열고 Time.timeScale을 0으로 설정한다. 기존 채취 기록·소지 개수·모델 비활성화는 유지한다. 시작 설정에 이미 채취된 시료는 신규 채취 이벤트를 발생시키지 않으므로 팝업도 열리지 않는다. 계속하기, Esc 또는 Backspace로 닫으면 이전 시간 배율과 입력 컨텍스트를 복원한다. 창이 열린 동안 일반 일시정지 메뉴의 중첩을 막는다. 임무 종료 상태에서는 시간을 재개하지 않는다. NASA 원문 버튼을 누르면 외부 브라우저에서 출처를 열며 팝업을 닫을 때까지 게임은 정지한다.

`Space HUD / SampleCollectionPopup`의 Samples 배열이 Index 0·1·2에 대응한다. 설명·분류·장소·URL·한영 번역은 `Assets/Research/SampleInformation/Apollo … .asset`에서 수정한다. 게임의 달 남극 배치는 실제 아폴로 채취 위치를 뜻하지 않으며 팝업에도 표시한다.

밝기는 같은 폴더의 `… Readable-0.mat`에서 조절한다. 원본 텍스처를 Base Map과 Emission Map에 함께 연결하고, Emission Color RGB 0.4로 약한 자체 발광을 더했다. URP Lit이므로 태양빛에 따른 음영은 남지만 빛이 없는 부분도 표면 무늬를 볼 수 있다. 원본 OBJ/JPG/MTL·임포트 설정은 변경하지 않았다. 자체 발광은 게임의 가독성을 위한 미술 처리이며 실제 암석의 발광 성질이 아니다.

## 출처

NASA / ARES, Astromaterials 3D. 2026-11-15 확인. 문구는 어린이를 위한 요약·번역이다.

- https://ares.jsc.nasa.gov/astromaterials3d/sample-details.htm?sample=15016-0
- https://ares.jsc.nasa.gov/astromaterials3d/sample-details.htm?sample=65035-0
- https://ares.jsc.nasa.gov/astromaterials3d/sample-details.htm?sample=76535-0

세 페이지가 실제로 사용하는 공식 설명 데이터: https://ares.jsc.nasa.gov/astromaterials3d/explorer/lunar_sample_metadata.json
사용한 세 항목의 원문은 `QA/SampleDiscovery/nasa-source-excerpts.json`에 보존했다. 게임은 설명 에셋을 읽으며 웹 호출이 필요 없다.

검증 메뉴: `Tools / Research / Validate Sample Popups`. 가상 키보드 채취·시간 정지/재개·일반 Pause 복귀·한영 720p/1080p를 확인한다. 결과와 검증 범위는 `QA/SampleDiscovery/VALIDATION.md`를 따른다.
