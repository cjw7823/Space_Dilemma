using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways, DisallowMultipleComponent]
public sealed class LunarEnvironment : MonoBehaviour
{
    [Header("References")]
    public Material skyTemplate;
    public Light sunlight;
    [Header("Sun (Moon Trek data or manual)")]
    public TextAsset sunAngleCsv;
    public bool useSunAngleData;
    [Min(0)] public float sunHoursFromStart;
    public bool animateSun;
    [Tooltip("Recorded hours per real second, only in Play mode. Stops at the last sample.")]
    [Min(0)] public float sunHoursPerSecond = 0.02f;
    [InspectorName("Sun Update Seconds")]
    [Tooltip("Seconds between Sun disk and directional-light updates during playback. Earth follows the continuous game clock. Set to 0 for continuous Sun movement.")]
    [Min(0f)] public float shadowUpdateSeconds;
    [Tooltip("World yaw of local true north. South polar stereographic map: atan2(projected X, projected Y).")]
    public float trueNorthYaw;
    [Tooltip("Manual world azimuth: 0 = +Z, 90 = +X. CSV azimuth is clockwise from true north.")]
    [Range(0, 360)] public float sunAzimuth = 180;
    [Range(-90, 90)] public float sunElevation = 8;
    [Range(0.1f, 2)] public float sunDiameter = 0.53f;
    [Header("Earth and stars ephemeris (offline SPICE)")]
    public bool useCelestialEphemeris;
    public TextAsset celestialEphemeris;
    public Cubemap starCubemap;
    [Header("Earth manual mode (artistic placement)")]
    [Range(0, 360)] public float earthAzimuth = 15;
    [Range(-90, 90)] public float earthElevation = 15;
    [Range(0.5f, 12)] public float earthDiameter = 4;
    [Range(0, 360)] public float earthMapRotation = 30;
    [SerializeField, HideInInspector] Quaternion earthOrientation = Quaternion.identity;
    [SerializeField, HideInInspector] Quaternion earthPlacement = Quaternion.identity;
    [Range(0, 4)] public float earthBrightness = 1.6f;
    [Header("Stars (illustrative design)")]
    public bool showStars = true;
    [ColorUsage(false)] public Color starTint = Color.white;
    [Tooltip("Artistic visibility control, independent of the sunlit ground exposure. Not a real star catalogue.")]
    [Range(0, 4)] public float illustrativeStars = 1;
    [Tooltip("Editor bake setting. Apply the star design in the Inspector after changing size, count or seed.")]
    [Range(0.25f, 3)] public float starSizeScale = 1;
    [Range(0, 12000)] public int starCount = 6000;
    public int starSeed = 20261114;
    [SerializeField, HideInInspector] float bakedStarSizeScale = 1;
    [SerializeField, HideInInspector] int bakedStarCount = 6000;
    [SerializeField, HideInInspector] int bakedStarSeed = 20261114;
    public bool StarDesignNeedsBake => !starCubemap || !Mathf.Approximately(starSizeScale,bakedStarSizeScale) ||
        starCount != bakedStarCount || starSeed != bakedStarSeed;

    public void RecordStarDesignApplied(Cubemap cube)
    {
        starCubemap=cube; bakedStarSizeScale=starSizeScale; bakedStarCount=starCount; bakedStarSeed=starSeed;
        needsApply=true;
    }
    [Header("Surface lighting")]
    [Range(0, 3)] public float sunIntensity = 1.25f;
    [Tooltip("Small artistic fill to keep the base readable; not lunar atmospheric scattering.")]
    [Range(0, 0.3f)] public float shadowFill = 0.18f;
    public Color sunlightColor = Color.white;

    public Quaternion EarthOrientation { get => SafeRotation(earthOrientation); set => earthOrientation = SafeRotation(value); }
    public Quaternion EarthPlacement { get => SafeRotation(earthPlacement); set => earthPlacement = SafeRotation(value); }
    public Vector3 EarthDirection => EphemerisActive ? celestialFrame.earthDirection : EarthPlacement * Direction(earthAzimuth, earthElevation);
    public float AppliedEarthDiameter => EphemerisActive ? celestialFrame.earthDiameter : earthDiameter;
    public Quaternion AppliedEarthOrientation => EphemerisActive ? celestialFrame.earthOrientation : EarthOrientation;
    public bool EphemerisActive { get; private set; }
    public string CelestialDataError { get; private set; }
    public LunarCelestialTable CelestialTable { get { LoadCelestialTable(); return celestialTable; } }
    public LunarCelestialTable.Frame CelestialFrame => celestialFrame;
    [System.NonSerialized] LunarCelestialTable celestialTable;
    [System.NonSerialized] TextAsset cachedCelestial;
    LunarCelestialTable.Frame celestialFrame;
    public Vector3 SunDirection { get; private set; }
    public float AppliedAzimuth { get; private set; }
    public float AppliedElevation { get; private set; }
    public string SunDataError { get; private set; }
    public SunAngleTable SunTable { get { LoadSunTable(); return sunTable; } }
    // Unity hot reload preserves private Unity objects/strings, but cannot preserve this parsed class.
    // Keep the entire cache transient so a reload cannot leave a false cache hit with a null table.
    [System.NonSerialized] SunAngleTable sunTable;
    [System.NonSerialized] TextAsset cachedCsv;

    Material sky;
    Material sourceSky;
    Material previousSky;
    Light previousSun;
    AmbientMode previousMode;
    Color previousAmbient;
    float previousReflection;
    bool previousFog;
    bool captured;
    bool needsApply;

    public static Vector3 Direction(float azimuth, float elevation)
    {
        float az = azimuth * Mathf.Deg2Rad, el = elevation * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
    }

    void OnEnable()
    {
        previousSky = RenderSettings.skybox;
        previousSun = RenderSettings.sun;
        previousMode = RenderSettings.ambientMode;
        previousAmbient = RenderSettings.ambientLight;
        previousReflection = RenderSettings.reflectionIntensity;
        previousFog = RenderSettings.fog;
        captured = true;
        Apply();
    }

    void OnValidate() { needsApply = true; }
    void LateUpdate()
    {
        if (Application.isPlaying && useSunAngleData && animateSun && SunTable != null)
            sunHoursFromStart = Mathf.Min((float)sunTable.DurationHours, sunHoursFromStart + Time.deltaTime * sunHoursPerSecond);
        // Also honors runtime changes made by other scripts, without allocating a material every frame.
        if (Application.isPlaying || needsApply) { Apply(); needsApply = false; }
    }

    static Quaternion SafeRotation(Quaternion q)
    {
        float n = q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w;
        if (float.IsNaN(n) || float.IsInfinity(n) || n < 0.000001f) return Quaternion.identity;
        return q.normalized;
    }

    public void RotateEarth(Vector3 axis, float degrees, Space space = Space.Self)
    {
        if (axis.sqrMagnitude < 0.000001f) return;
        Quaternion delta = Quaternion.AngleAxis(degrees, axis.normalized);
        EarthOrientation = space == Space.Self ? EarthOrientation * delta : delta * EarthOrientation;
        Apply();
    }

    void LoadSunTable()
    {
        if (cachedCsv == sunAngleCsv &&
            (sunTable != null || SunDataError != null || !sunAngleCsv)) return;
        cachedCsv = sunAngleCsv;
        sunTable = null;
        SunDataError = null;
        if (sunAngleCsv && !SunAngleTable.TryParse(sunAngleCsv.text, out sunTable, out string error)) SunDataError = error;
    }

    void LoadCelestialTable()
    {
        // TextAsset.text allocates a managed string. Never copy the 470 KB document every frame.
        if (cachedCelestial == celestialEphemeris &&
            (celestialTable != null || CelestialDataError != null || !celestialEphemeris)) return;
        cachedCelestial = celestialEphemeris;
        celestialTable = null; CelestialDataError = null;
        if (celestialEphemeris && !LunarCelestialTable.TryParse(celestialEphemeris.text, out celestialTable, out string error)) CelestialDataError = error;
    }

    public void InvalidateDataCaches()
    {
        cachedCsv=null; sunTable=null; SunDataError=null;
        cachedCelestial=null; celestialTable=null; CelestialDataError=null;
        needsApply=true;
    }

    void EvaluateCelestialFrame()
    {
        EphemerisActive = false;
        if (!useCelestialEphemeris) return;
        LoadCelestialTable();
        if (celestialTable == null) return;
        LoadSunTable();
        double offset = sunTable != null ? (sunTable.StartUtc-celestialTable.StartUtc).TotalSeconds : 0;
        celestialFrame = celestialTable.Evaluate(offset+sunHoursFromStart*3600.0);
        EphemerisActive = true;
    }

    public Vector3 EvaluateSunDirection()
    {
        AppliedAzimuth = sunAzimuth;
        AppliedElevation = sunElevation;
        if (useSunAngleData)
        {
            LoadSunTable();
            if (sunTable != null)
            {
                var sample = sunTable.Evaluate(EvaluateSunSampleHours());
                AppliedAzimuth = sample.azimuth;
                AppliedElevation = sample.elevation;
                return Direction(AppliedAzimuth + trueNorthYaw, AppliedElevation);
            }
        }
        return Direction(AppliedAzimuth, AppliedElevation);
    }

    public void Apply()
    {
        // Always clone: editing this component must not overwrite the shared sky asset.
        if (sky && sourceSky != skyTemplate) ReleaseSky();
        if (!sky && skyTemplate)
        {
            sky = new Material(skyTemplate) { name = "Lunar Sky (instance)", hideFlags = HideFlags.HideAndDontSave };
            sourceSky = skyTemplate;
        }
        Vector3 sun = SunDirection = EvaluateSunDirection();
        EvaluateCelestialFrame();
        if (sky)
        {
            sky.CopyPropertiesFromMaterial(skyTemplate);
            sky.SetVector("_SunDirection", sun);
            sky.SetVector("_EarthDirection", EarthDirection);
            // Rotate the spherical surface before equirectangular projection. Lighting stays in world space.
            sky.SetMatrix("_EarthWorldToTexture", Matrix4x4.Rotate(Quaternion.Inverse(AppliedEarthOrientation)));
            sky.SetMatrix("_WorldToStars", Matrix4x4.Rotate(EphemerisActive ? Quaternion.Inverse(celestialFrame.starsOrientation) : Quaternion.identity));
            sky.SetVector("_EarthSunDirection", EphemerisActive ? celestialFrame.earthSunDirection : sun);
            sky.SetFloat("_UseEphemeris", EphemerisActive ? 1 : 0);
            sky.SetTexture("_StarCube", starCubemap);
            sky.SetFloat("_SunDiameter", sunDiameter);
            sky.SetFloat("_EarthDiameter", AppliedEarthDiameter);
            sky.SetFloat("_EarthRotation", EphemerisActive ? 0 : earthMapRotation);
            sky.SetFloat("_EarthBrightness", earthBrightness);
            sky.SetFloat("_StarBrightness", showStars ? illustrativeStars : 0);
            sky.SetColor("_StarTint", starTint);
            RenderSettings.skybox = sky;
        }
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(shadowFill, shadowFill, shadowFill);
        RenderSettings.reflectionIntensity = 0.15f;
        if (sunlight)
        {
            Quaternion lightRotation = Quaternion.LookRotation(-sun);
            if (!sunlight.transform.rotation.Equals(lightRotation))
                sunlight.transform.rotation = lightRotation;
            sunlight.color = sunlightColor;
            sunlight.useColorTemperature = false;
            sunlight.intensity = AppliedElevation > -sunDiameter * 0.5f ? sunIntensity : 0;
            RenderSettings.sun = sunlight;
        }
    }

    double EvaluateSunSampleHours()
    {
        if (!Application.isPlaying || !animateSun || !useSunAngleData || !(shadowUpdateSeconds > 0f) ||
            !(sunHoursPerSecond > 0f) || sunTable == null || sunHoursFromStart >= sunTable.DurationHours)
            return sunHoursFromStart;

        double stepHours = (double)sunHoursPerSecond * shadowUpdateSeconds;
        return System.Math.Floor(sunHoursFromStart / stepHours) * stepHours;
    }

    void ReleaseSky()
    {
        if (sky) { if (Application.isPlaying) Destroy(sky); else DestroyImmediate(sky); }
        sky = null;
    }

    [ContextMenu("Preview preset (illustrative)")]
    public void PreviewPreset()
    {
        useSunAngleData = false;
        useCelestialEphemeris = false;
        sunAzimuth = 180; sunElevation = 8; earthElevation = 15; earthDiameter = 4; shadowFill = 0.18f;
        Apply();
    }

    [ContextMenu("Polar scale reference (not a Site6 ephemeris)")]
    public void PolarReference()
    {
        useSunAngleData = false;
        useCelestialEphemeris = false;
        sunElevation = 1.5f; earthElevation = 3; earthDiameter = 1.9f; shadowFill = 0.025f;
        Apply();
    }

    void OnDisable()
    {
        if (captured && RenderSettings.skybox == sky)
        {
            RenderSettings.skybox = previousSky;
            RenderSettings.sun = previousSun;
            RenderSettings.ambientMode = previousMode;
            RenderSettings.ambientLight = previousAmbient;
            RenderSettings.reflectionIntensity = previousReflection;
            RenderSettings.fog = previousFog;
        }
        ReleaseSky();
    }
}
