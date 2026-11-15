# Space Dilemma

[한국어 README](README_KOR.md)

Space Dilemma is a short lunar base simulation. Explore on foot or by rover, collect and store lunar samples, install scientific instruments, and start plant experiments. When a solar particle event increases radiation exposure, decide how much of the base's limited power to divert from research to shielding.

## Main datasets

### Radiation and solar particle events

- **NASA CCMC DONKI** provides the observation time used to align the radiation data. The current radiation model uses the solar energetic particle event (SEP) recorded on **August 27, 2022, at 11:12 UTC**. The mission compresses 48 game hours into 10 minutes of play, with the event starting at 38 hours 24 minutes in the game, or minute 8 for the player. [NASA CCMC DONKI](https://ccmc.gsfc.nasa.gov/tools/DONKI/)
- **NASA RadLab** provides absorbed dose rates from the Lunar Reconnaissance Orbiter's CRaTER-D1D2 detector. The analysis checks observation coverage and averages the 48 hours on each side of the DONKI timestamp. Unity uses **11.4664 μGy/h before** and **65.1237 μGy/h after** as the basis for its two outdoor rates. It multiplies both averages by the gameplay factor `k = 570` and divides by 1,000 to get game mSv per game hour. [NASA RadLab Data API](https://visualization.osdr.nasa.gov/radlab/gui/data-api/)

The Python analyzer can also compare candidate events, export minute-by-minute estimates, and calculate a gameplay factor for a target cumulative dose. The default scenario uses the two averages, with a **600 game mSv** limit and reduced exposure inside the base. [Radiation Analyzer README](MoonData/Radiation_Analyzer/README.md) · [Game calibration](Assets/Survival/RADLAB_CALIBRATION.md)

### Lunar terrain, sunlight, and Earth

- **Nobile Rim 1 terrain.** The terrain tool converts NASA's 5 m/pixel elevation GeoTIFF into 16-bit RAW heightmap tiles for Unity and records their geographic bounds and elevation ranges. [NASA Goddard elevation data](https://pgda.gsfc.nasa.gov/products/104)
- **NASA Moon Trek solar angles.** The current scene uses solar elevation and azimuth from March 21–23, 2024 UTC to set the direction of sunlight. This sky timeline and the 2022 radiation observations come from separate periods and advance on the same mission clock. [NASA Moon Trek](https://trek.nasa.gov/moon/) · [Lunar environment notes](Assets/MoonEnvironment/README.md)
- **NASA/JPL SPICE and Horizons.** Development tools sample celestial geometry every five minutes from an observer placed at the center of the elevation model. Unity uses these calculations to place, orient, size, and light Earth in the sky. The tools cross-check Earth's apparent size with Horizons. [NASA/JPL SPICE](https://naif.jpl.nasa.gov/naif/) · [JPL Horizons](https://ssd.jpl.nasa.gov/horizons/)

The Python tools prepare and verify these inputs before build time. The game uses the packaged data at runtime. [Terrain and Celestial Tools README](MoonData/SunAngle/README.md)

## How to run

1. Download `Build.zip` and extract the entire archive on Windows.
2. Open the extracted build folder and run `Space_Dilemma.exe`. Keep the executable with the other extracted files and folders.
3. Choose **Start Game** and close the radiation briefing to begin the mission.

## Controls

### Keyboard and mouse

Click inside the game to take control.

| Action | Input |
| --- | --- |
| Walk or drive the rover | **WASD** or **arrow keys** |
| Look around | **Mouse** |
| Sprint or jump on foot | **Left Shift** or **Space**, respectively |
| Board or exit the rover | **T** |
| Collect or store a sample, install an instrument, or start a plant experiment | Hold **E** near the target |
| Open the base power console | Press **E** near the console |
| Enter or leave scenery view | **L** |
| Pause or release the mouse cursor | **Esc** to pause, **Tab** to release the cursor |

### Custom gamepad via USB Serial

The custom gamepad connects through **USB Serial (COM)**. USB HID mode is unimplemented for this device. Without a serial connection, the game defaults to **keyboard and mouse**.

Connect the controller by USB. In the game's **Options**, select or enter its COM port, set the baud rate to match the controller (default **115200**), and choose **Connect / Reconnect**.

| Action | Input |
| --- | --- |
| Walk, drive, or navigate menus | **Joystick 1** |
| Look around or rotate the scenery camera left and right | **Joystick 2** |
| Board or exit the rover, or confirm a menu selection | **Button 0** |
| Jump on foot or go back in supported menus | **Button 1** |
| Open the input examples menu | **Button 2** |
| Sprint on foot | **Button 3** |
| Enter scenery view and adjust camera height | Turn the **round knob** from below **10** to **10** or higher. Values from **10 to 1000** control height. Turn it below **10** to return. |

Use the keyboard for **E** research and power-console interactions and **Esc** to pause when playing with the custom gamepad.

## Additional NASA data and scientific references

This document summarizes the **terrain, lunar samples, and mission parameters** drawn from NASA public sources and how they are used in the game. Unless otherwise stated, the values below **were not measured at an actual lunar base**.

### 1. Lunar Landing Candidate Region & Terrain

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

### 2. Actual Lunar Rock Samples

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

### 3. Space Radiation Standard

**NASA-STD-3001 — 600 mSv Career Exposure Limit**
- Description: A **career cumulative effective-dose** standard for astronauts, not a limit for a single EVA or a threshold for immediate health effects. It cannot be compared directly with a game's sensor readings.
- Reference: [9] NASA, “Spaceflight Human-System Standard, Volume 1, Sec. 4.8.2 (V1 4030).” [Online]. Available: https://www.nasa.gov/reference/4-0-human-performance/

### 4. Mission Parameters & Sources

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

### 5. Data Attribution & Usage

- Distinguish NASA **public datasets**, **derived calculations**, and **game simulations**. Mission failure conditions, equipment failures, and plant growth outcomes are gameplay rules unless separately validated.
- For model and texture licenses, see [Third-party notices](Assets/StreamingAssets/Space_Dilemma_THIRD_PARTY_NOTICES.md).
- Reference: [14] NASA, “NASA Media Usage Guidelines.” [Online]. Available: https://www.nasa.gov/nasa-brand-center/images-and-media/

*Space_Dilemma is not affiliated with, sponsored by, or endorsed by NASA.*
