# Space_Dilemma — NASA Data & Scientific References

This document summarizes the **terrain, lunar samples, and mission parameters** drawn from NASA public sources and how they are used in the game. Unless otherwise stated, the values below **were not measured at an actual lunar base**.

## 1. Lunar Landing Candidate Region & Terrain

**Artemis III — Nobile Rim 1 Candidate Landing Region**
- Description: One of nine candidate regions announced by NASA for a crewed lunar landing. **It is not a confirmed site for a permanent lunar base.**
- Reference: [1] NASA, “NASA Provides Update on Artemis III Moon Landing Regions,” 2024. [Online]. Available: https://www.nasa.gov/news-release/nasa-provides-update-on-artemis-iii-moon-landing-regions

**PGDA Product 78 — Site06 / Nobile Rim 1**
- Description: Source identifying `Site06` as Nobile Rim 1 and documenting LRO/LOLA-based elevation data at 5 m/pixel.
- Reference: [2] NASA Goddard PGDA, “High-Resolution LOLA Topography for Lunar South Pole Sites.” [Online]. Available: https://pgda.gsfc.nasa.gov/products/78

**VIPER — Scientific Rationale for the Nobile Region**
- Description: Explains the scientific value of exploring the Nobile region. This concerns **a different mission** from the Artemis III landing-site selection.
- Reference: [3] NASA, “NASA's Artemis Rover to Land Near Nobile Region of Moon's South Pole.” [Online]. Available: https://www.nasa.gov/news-release/nasas-artemis-rover-to-land-near-nobile-region-of-moons-south-pole

**PGDA Product 104 — Lunar Surface Elevation Data**
- Description: A 5 m/pixel shape-from-shading digital elevation model (SDEM) converted into Unity Terrain heightmaps. This does **not** mean NASA has validated the game's terrain rendering, lighting, or physics.
- Reference: [4] NASA Goddard PGDA, “Enhanced Topography Models for Selected Lunar South Pole Regions with Shape-from-Shading.” [Online]. Available: https://pgda.gsfc.nasa.gov/products/104

**NASA Moon Trek — Lunar Terrain Visualization**
- Description: Visual reference for lunar terrain, locations, and lighting conditions; not evidence of an exact solar elevation angle at a specific time.
- Reference: [5] NASA, “Moon Trek.” [Online]. Available: https://trek.nasa.gov/moon/#v=0.1&x=130770.45593261695&y=40368.27117919925&z=5&p=urn%3Aogc%3Adef%3Acrs%3AIAU2000%3A%3A30120&d=&locale=&b=moon&e=-95853.74084472646%2C-134081.5237426755%2C357394.65270996036%2C214818.066101074&sfz=&w=

## 2. Actual Lunar Rock Samples

**Apollo 15 — Sample 15016,0**
- Description: Reference for a real Apollo 15 lunar sample and its 3D model.
- Reference: [6] NASA Johnson Space Center, “Astromaterials 3D: Sample 15016,0.” [Online]. Available: https://ares.jsc.nasa.gov/astromaterials3d/sample-details.htm?sample=15016-0

**Apollo 16 — Sample 65035,0**
- Description: Reference for a real Apollo 16 lunar sample and its 3D model.
- Reference: [7] NASA Johnson Space Center, “Astromaterials 3D: Sample 65035,0.” [Online]. Available: https://ares.jsc.nasa.gov/astromaterials3d/sample-details.htm?sample=65035-0

**Apollo 17 — Sample 76535,0**
- Description: Reference for a real Apollo 17 lunar sample and its 3D model.
- Reference: [8] NASA Johnson Space Center, “Astromaterials 3D: Sample 76535,0.” [Online]. Available: https://ares.jsc.nasa.gov/astromaterials3d/sample-details.htm?sample=76535-0

> These three samples **were not collected from Nobile Rim 1**.

## 3. Space Radiation Standard

**NASA-STD-3001 — 600 mSv Career Exposure Limit**
- Description: A **career cumulative effective-dose** standard for astronauts, not a limit for a single EVA or a threshold for immediate health effects. It cannot be compared directly with a game's sensor readings.
- Reference: [9] NASA, “Spaceflight Human-System Standard, Volume 1, Sec. 4.8.2 (V1 4030).” [Online]. Available: https://www.nasa.gov/reference/4-0-human-performance/

## 4. Mission Parameters & Sources

**Mission 01 — Lunar Root Growth Experiment**
- Description: Combines **735 W** (ISS Plant Habitat design estimate) and **28 hours 40 minutes** (combined phases of an ISS Gravi-1 experiment) to calculate a **21.07 kWh gameplay energy budget**. This is not a measurement from an actual lunar experiment.
- Reference: [10] B. Granath, “Kennedy engineers designing plant habitat for space station,” NASA, Feb. 19, 2013. [Online]. Available: https://www.nasa.gov/science-research/kennedy-engineers-designing-plant-habitat-for-space-station/
- Reference: [11] C. A. Evans *et al*., *International Space Station Science Research Accomplishments During the Assembly Years: An Analysis of Results from 2000–2008*, NASA/TP–2009–213146, Rev. A, “Threshold Acceleration for Gravisensing (Gravi),” p. 94, 2009. [Online]. Available: https://ntrs.nasa.gov/citations/20090029998

**Mission 02 — EVA Sample Cryogenic Storage**
- Description: **395 W** is the inverter input power at one operating point of a cryocooler evaluated for FROSTE (11 W net cooling capacity at 80 K). It is not the total power of a storage facility or a value applicable to every operating condition.
- Reference: [12] J. A. Sanford *et al*., “FROSTE: Frozen Return of Samples to Earth,” poster presented at the Jamboree and Poster Expo, Huntsville, AL, USA, May 16, 2024, NASA Technical Reports Server. [Online]. Available: https://ntrs.nasa.gov/citations/20240004448

**Mission 03 — EVA Scientific Instrument Deployment**
- Description: **29 W / 52 kg** is the combined power and mass of six proposed instruments (seismometer, heat-flow probe, magnetometer, ejecta and meteorite experiment, electromagnetic sounder, and neutron detector). **Additional loads for communications, control, heaters, etc., are excluded.**
- Reference: [13] J. Edmunson and H. Haviland, “Science of the Moon,” Sec. 8, Ch. 46, Table 1.5, *Handbook of Lunar Base Design and Development*, 2020, NASA Technical Reports Server. [Online]. Available: https://ntrs.nasa.gov/citations/20205004514

## 5. Data Attribution & Usage

- Distinguish NASA **public datasets**, **derived calculations**, and **game simulations**. Mission failure conditions, equipment failures, and plant growth outcomes are gameplay rules unless separately validated.
- For model and texture licenses, see `Assets/StreamingAssets/THIRD_PARTY_NOTICES.md`.
- Reference: [14] NASA, “NASA Media Usage Guidelines.” [Online]. Available: https://www.nasa.gov/nasa-brand-center/images-and-media/

*Space_Dilemma is not affiliated with, sponsored by, or endorsed by NASA.*
