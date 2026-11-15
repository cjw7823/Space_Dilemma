# Space_Dilemma — NASA Data & Scientific References

NASA 공개 자료를 활용한 **지형·시료·미션 수치**와 게임 내 적용 방식을 정리합니다. 아래 수치는 별도 표기가 없는 한 **실제 달 기지에서 측정한 값이 아닙니다.**

## 1. 달 착륙 후보지와 지형

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

## 2. 실제 달 암석 시료

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

## 3. 우주방사선 기준

**NASA-STD-3001 — 경력 누적 선량 600 mSv**
- 설명: 우주비행사의 **경력 누적 유효선량** 기준. 단일 EVA 선량이나 즉각적인 건강 이상 임계값이 아니며, 게임 센서 선량과 직접 비교할 수 없음.
- 출처: [9] NASA, “Spaceflight Human-System Standard, Volume 1, Sec. 4.8.2 (V1 4030).” [Online]. Available: https://www.nasa.gov/reference/4-0-human-performance/

## 4. 미션 수치와 근거

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

## 5. 자료 사용 원칙

- NASA **공개 데이터**, 자료 기반 **계산값**, **게임 시뮬레이션**을 구분합니다. 임무 실패 조건·장비 고장·식물 생장 결과는 별도 검증 전까지 게임 규칙입니다.
- 모델·텍스처의 라이선스는 `Assets/StreamingAssets/THIRD_PARTY_NOTICES.md` 참조.
- 출처: [14] NASA, “NASA Media Usage Guidelines.” [Online]. Available: https://www.nasa.gov/nasa-brand-center/images-and-media/

*Space_Dilemma is not affiliated with, sponsored by, or endorsed by NASA.*
